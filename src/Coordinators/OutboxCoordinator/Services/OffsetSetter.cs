using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NewRelic.Api.Agent;
using PollingOutboxPublisher.Coordinators.OutboxCoordinator.Services.Interfaces;
using PollingOutboxPublisher.Database.Repositories.Interfaces;
using PollingOutboxPublisher.Models;
using PollingOutboxPublisher.Exceptions;

namespace PollingOutboxPublisher.Coordinators.OutboxCoordinator.Services;

[ExcludeFromCodeCoverage]
public class OffsetSetter : IOffsetSetter
{
    private readonly IOutboxOffsetRepository _outboxOffsetRepository;
    private readonly ILogger<OffsetSetter> _logger;

    public OffsetSetter(ILogger<OffsetSetter> logger, IOutboxOffsetRepository outboxOffsetRepository)
    {
        _logger = logger;
        _outboxOffsetRepository = outboxOffsetRepository;
    }

    [Trace]
    public async Task SetLatestOffset(OutboxEvent[] items)
    {
        try
        {
            var latestOffSet = items.Max(row => row.Id);
            await _outboxOffsetRepository.UpdateOffsetAsync(latestOffSet);
            _logger.LogInformation("LatestOffSet: {LatestOffSet}", latestOffSet);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update offset in database");
            throw new DatabaseOperationException("Failed to update offset", ex);
        }
    }
}