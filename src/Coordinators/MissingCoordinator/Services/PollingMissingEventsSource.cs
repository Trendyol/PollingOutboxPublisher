using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PollingOutboxPublisher.Coordinators.MissingCoordinator.Services.Interfaces;
using PollingOutboxPublisher.Database.Repositories.Interfaces;
using PollingOutboxPublisher.Extensions;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Coordinators.MissingCoordinator.Services;

public class PollingMissingEventsSource : IPollingMissingEventsSource
{
    private readonly ILogger<PollingMissingEventsSource> _logger;
    private readonly IMissingEventRepository _missingEventRepository;
    private readonly IOutboxEventRepository _outboxEventRepository;

    public PollingMissingEventsSource(ILogger<PollingMissingEventsSource> logger,
        IMissingEventRepository missingEventRepository,
        IOutboxEventRepository outboxEventRepository)
    {
        _logger = logger;
        _missingEventRepository = missingEventRepository;
        _outboxEventRepository = outboxEventRepository;
    }

    public async Task<MissingEvent[]> GetMissingEventsAsync(int batchCount)
    {
        var missingEvents = await _missingEventRepository.GetMissingEventsAsync(batchCount);
        return missingEvents.IsNullOrEmpty() ? Array.Empty<MissingEvent>() : missingEvents.ToArray();
    }

    public async Task<OutboxEvent[]> GetNextOutboxEventsAsync(MissingEvent[] retryableMissingEvents)
    {
        var retryableMissingEventsIds = retryableMissingEvents.Select(x => x.Id).ToArray();

        _logger.LogInformation("Missing events found. MissingEventCount: {MissingEventCount}",
            retryableMissingEvents.Length);

        var outboxEvents = await _outboxEventRepository.GetOutboxEventsAsync(retryableMissingEventsIds);

        _logger.LogInformation("Retryable events found. " +
                               "OutboxEventCount: {OutboxEventCount}, " +
                               "MissingEventCount: {MissingEventCount}",
            outboxEvents.Count, retryableMissingEvents.Length);

        return outboxEvents.ToArray();
    }
}