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
     * Because of our outbox algorithm and MsSql async transactions, there may be some times when we process last message which
     * row id: 500, product-api still may working on the transaction it occurred for id: 450 and not commit it yet. In our EventWorker
     * when we mark "500" as last id we processed, we are missing 450 which are committed after we processed 500. For this reason we are dedecting
     * these missing ids while processing them in EventWorker and later recover them here. Since event orders are not important for our domain,
     * we don't block bulk of messages because there is still missing an event.
     * Here is the algorithm;
     * 1. EventWorker detects gap in its batch process, e.g: for events (1,2,5,6) missing events are (3,4)
     * 2. Missing events are written to MissingOutboxEvents table with the date it is missed
     * 3. MissingEventsWorker reads missing events from MissingOutboxEvents table with configured batchSize
     * 4. Filter out events which retry time is exceed. Max retry duration is configurable from application.properties
     * 5. For each retry time exceed event:
     *  5.1. Delete events from MissingOutboxEvents table
     *  5.2. Publish event to earth.product.pim.product-event-publisher.missing-events.0 topic for logging purpose
     * 6. For each retryable events which retry time is not exceed:
     *  6.1. Publish event to corresponding outbox topic
     *  6.2. Delete events from MissingOutboxEvents table
     */
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && await _masterPodChecker.IsMasterPodAsync(cancellationToken))
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