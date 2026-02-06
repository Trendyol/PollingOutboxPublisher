using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PollingOutboxPublisher.Coordinators.OutboxCoordinator.Services.Interfaces;
using PollingOutboxPublisher.Database.Repositories.Interfaces;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Coordinators.OutboxCoordinator.Services;

public class PollingSource : IPollingSource
{
    private readonly ILogger<PollingSource> _logger;
    private readonly IOutboxOffsetRepository _outboxOffsetRepository;
    private readonly IOutboxEventRepository _outboxEventRepository;
    private const int MillisecondsDelay = 200;

    public PollingSource(ILogger<PollingSource> logger, IOutboxOffsetRepository outboxOffsetRepository,
        IOutboxEventRepository outboxEventRepository)
    {
        _logger = logger;
        _outboxOffsetRepository = outboxOffsetRepository;
        _outboxEventRepository = outboxEventRepository;
    }

    public async Task<OutboxEventsBatch> GetNextAsync(int batchCount)
    {
        var outboxEventsBatch = new OutboxEventsBatch();

        var latestOffset = await _outboxOffsetRepository.GetLatestOffsetAsync();
        if (!latestOffset.HasValue)
        {
            _logger.LogError("Latest Offset doesn't found");
            return outboxEventsBatch;
        }

        var newestEventId = await _outboxEventRepository.GetNewestEventIdAsync(latestOffset!.Value);
        if (newestEventId <= latestOffset)
        {
            await Task.Delay(MillisecondsDelay);
            return outboxEventsBatch;
        }

        var latestEventList =
            await _outboxEventRepository.GetLatestEventsAsync(batchCount, newestEventId);

        outboxEventsBatch.LatestOffset = latestOffset;
        outboxEventsBatch.Events = latestEventList.ToArray();

        return outboxEventsBatch;
    }
}