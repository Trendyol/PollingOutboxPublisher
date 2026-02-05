using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using PollingOutboxPublisher.ConfigOptions;
using PollingOutboxPublisher.Coordinators.MissingCoordinator.Services.Interfaces;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Coordinators.MissingCoordinator.Services;

public class PollingMissingEventsQueue : IPollingMissingQueue
{
    private readonly IPollingMissingEventsSource _pollingMissingEventsSource;
    private readonly int _waitDuration;
    private readonly int _batchSize;
    private readonly int _retryLimit;

    public PollingMissingEventsQueue(IPollingMissingEventsSource pollingMissingEventsSource,
        IOptions<WorkerSettings> outboxSettings)
    {
        _pollingMissingEventsSource = pollingMissingEventsSource;
        _waitDuration = outboxSettings.Value.MissingEventsWaitDuration;
        _batchSize = outboxSettings.Value.MissingEventsBatchSize;
        _retryLimit = outboxSettings.Value.MissingEventsMaxRetryCount;
    }

    public Task<(MissingEvent[], MissingEvent[], List<MappedMissingEvent>, OutboxEvent[])> DequeueAsync(
        CancellationToken cancellationToken)
    {
        return GetMissingTuples(cancellationToken);
    }

    private async Task<(MissingEvent[], MissingEvent[], List<MappedMissingEvent>, OutboxEvent[])> GetMissingTuples(CancellationToken cancellationToken)
    {
        var missingEvents = await _pollingMissingEventsSource.GetMissingEventsAsync(_batchSize);
        if (missingEvents.Length == 0 && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(_waitDuration, cancellationToken);
            return (null, null, null, null);
        }

        var retryableMissingEvents = missingEvents.Where(x => x.RetryCount < _retryLimit || x.ExceptionThrown).ToArray();
            
        var outboxEvents = await _pollingMissingEventsSource.GetNextOutboxEventsAsync(retryableMissingEvents);
        if (outboxEvents.Length == 0 && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(_waitDuration, cancellationToken);
            return (retryableMissingEvents, missingEvents, null, null);
        }

        var mappedMissingEvents = GetMappedMissingEvents(missingEvents, outboxEvents);
            
        return (retryableMissingEvents, missingEvents, mappedMissingEvents, outboxEvents);
    }

    private static List<MappedMissingEvent> GetMappedMissingEvents(IEnumerable<MissingEvent> missingEvents, IEnumerable<OutboxEvent> outboxEvents)
    {
        var mappedMissingEvents = missingEvents.Join(
            outboxEvents,
            item1 => item1.Id,
            item2 => item2.Id,
            (item1, item2) => new MappedMissingEvent { MissingEvent = item1, OutboxEvent = item2 }
        ).ToList();
        return mappedMissingEvents;
    }
}