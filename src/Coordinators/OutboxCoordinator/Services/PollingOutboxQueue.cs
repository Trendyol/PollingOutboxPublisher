using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using PollingOutboxPublisher.ConfigOptions;
using PollingOutboxPublisher.Coordinators.OutboxCoordinator.Services.Interfaces;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Coordinators.OutboxCoordinator.Services;

public class PollingOutboxQueue : IPollingQueue
{
    private readonly IPollingSource _pollingSource;
    private readonly IMissingDetector _missingDetector;
    private readonly int _waitDuration;
    private readonly int _prefetchCount;

    public PollingOutboxQueue(IPollingSource pollingSource, IOptions<WorkerSettings> workerSettings, IMissingDetector missingDetector)
    {
        _pollingSource = pollingSource;
        _missingDetector = missingDetector;
        _waitDuration = workerSettings.Value.QueueWaitDuration;
        _prefetchCount = workerSettings.Value.OutboxEventsBatchSize;
    }

    public async Task<OutboxEvent[]> DequeueAsync(CancellationToken cancellationToken)
    {
        var result = await _pollingSource.GetNextAsync(_prefetchCount);
        await _missingDetector.DetectAsync(result);
        await Task.Delay(_waitDuration, cancellationToken);
        return result.Events;
    }
}