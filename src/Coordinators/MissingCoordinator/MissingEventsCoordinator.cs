using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PollingOutboxPublisher.Coordinators.MissingCoordinator.Interfaces;
using PollingOutboxPublisher.Coordinators.MissingCoordinator.Services.Interfaces;
using PollingOutboxPublisher.Coordinators.Services;
using PollingOutboxPublisher.Coordinators.Services.Interfaces;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Coordinators.MissingCoordinator;

public class MissingEventsCoordinator : IMissingEventsCoordinator
{
    private readonly IOutboxDispatcher _outboxDispatcher;
    private readonly IPollingMissingQueue _pollingMissingQueue;
    private readonly IMissingEventCleaner _missingEventCleaner;
    private readonly IMasterPodChecker _masterPodChecker;
    private readonly ICircuitBreaker _circuitBreaker;

    public MissingEventsCoordinator(IOutboxDispatcher outboxDispatcher, IPollingMissingQueue pollingMissingQueue,
        IMissingEventCleaner missingEventCleaner, IMasterPodChecker masterPodChecker, ICircuitBreaker circuitBreaker)
    {
        _outboxDispatcher = outboxDispatcher;
        _pollingMissingQueue = pollingMissingQueue;
        _missingEventCleaner = missingEventCleaner;
        _masterPodChecker = masterPodChecker;
        _circuitBreaker = circuitBreaker;
    }

    /**
     * Due to the nature of asynchronous transactions in the outbox pattern, gaps in event sequences can occur.
     * For example: When processing event ID 500, event ID 450 might still be in an uncommitted transaction.
     * If we mark ID 500 as processed, we could miss ID 450 when it commits later.
     *
     * Since event ordering is not critical for our domain, we handle these gaps asynchronously:
     *
     * 1. EventWorker detects sequence gaps during batch processing
     *    Example: For events [1,2,5,6], missing events are [3,4]
     *
     * 2. Missing events are recorded in MissingOutboxEvents table with timestamp
     *
     * 3. MissingEventsWorker polls MissingOutboxEvents table using configured batch size
     *
     * 4. Events exceeding retry duration (configured in application.properties) are filtered
     *
     * 5. For expired events:
     *    - Remove from MissingOutboxEvents table
     *    - Log to 'missing-events' topic for auditing
     *
     * 6. For the valid event that has a matching outbox event:
     *    - Publish to original outbox topic
     *    - Remove from MissingOutboxEvents table
     */
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested &&
               await _masterPodChecker.IsMasterPodAsync(cancellationToken))
        {
            var (retryableMissingEvents, missingEvents, mappedMissingEvents, outboxEvents) =
                await _pollingMissingQueue.DequeueAsync(cancellationToken);

            await _missingEventCleaner.HandleNonMatchedMissingEventsAsync(outboxEvents, retryableMissingEvents);

            //means that outbox matching found, let's try publish
            if (mappedMissingEvents is not null)
            {
                var taskToAwait = mappedMissingEvents.Select(item => _outboxDispatcher.DispatchMissingAsync(item));
                var tasks = taskToAwait.ToArray();
                await Task.WhenAll(tasks);
                var resultOfMappedMissingEvents = GatherMappedMissingEventsFromTasks(tasks);
                await _missingEventCleaner.CleanMissingEventsHaveOutboxEventAsync(missingEvents,
                    resultOfMappedMissingEvents.ToList());
            }

            // move missing events that don't have match
            if (missingEvents is not null && outboxEvents is not null)
            {
                var missingEventsDontHaveMatch = missingEvents
                    .Where(missingEvent => outboxEvents.All(outboxEvent => outboxEvent.Id != missingEvent.Id))
                    .ToArray();
                await _missingEventCleaner.CleanMissingEventsNotHaveOutboxEventAsync(missingEventsDontHaveMatch);
            }
            else if (missingEvents is not null)
            {
                await _missingEventCleaner.CleanMissingEventsNotHaveOutboxEventAsync(missingEvents);
            }

            // Reset circuit breaker after successful processing
            _circuitBreaker.Reset();
        }
    }

    private static IEnumerable<MappedMissingEvent> GatherMappedMissingEventsFromTasks(
        IEnumerable<Task<MappedMissingEvent>> tasks)
    {
        return (from task in tasks where task.Status == TaskStatus.RanToCompletion select task.Result).ToList();
    }
}