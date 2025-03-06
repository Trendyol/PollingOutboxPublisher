using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PollingOutboxPublisher.Coordinators.OutboxCoordinator.Interfaces;
using PollingOutboxPublisher.Coordinators.OutboxCoordinator.Services.Interfaces;
using PollingOutboxPublisher.Coordinators.Services;
using PollingOutboxPublisher.Coordinators.Services.Interfaces;

namespace PollingOutboxPublisher.Coordinators.OutboxCoordinator;

public class OutboxCoordinator : IOutboxCoordinator
{
    private readonly IOutboxDispatcher _outboxDispatcher;
    private readonly IPollingQueue _pollingQueue;
    private readonly IOffsetSetter _offsetSetter;
    private readonly IMasterPodChecker _masterPodChecker;
    private readonly ICircuitBreaker _circuitBreaker;

    public OutboxCoordinator(IOutboxDispatcher outboxDispatcher, IPollingQueue pollingQueue,
        IOffsetSetter offsetSetter, IMasterPodChecker masterPodChecker,
        ICircuitBreaker circuitBreaker)
    {
        _outboxDispatcher = outboxDispatcher;
        _pollingQueue = pollingQueue;
        _offsetSetter = offsetSetter;
        _masterPodChecker = masterPodChecker;
        _circuitBreaker = circuitBreaker;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && await _masterPodChecker.IsMasterPodAsync(cancellationToken))
        {
            var outboxEvents = await _pollingQueue.DequeueAsync(cancellationToken);
            if (outboxEvents.Length == 0) continue;

            var taskToAwait = outboxEvents.Select(item => _outboxDispatcher.DispatchAsync(item));
            await Task.WhenAll(taskToAwait);

            await _offsetSetter.SetLatestOffset(outboxEvents);
            
            _circuitBreaker.Reset();
        }
    }
}