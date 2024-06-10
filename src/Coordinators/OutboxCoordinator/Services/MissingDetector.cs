using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PollingOutboxPublisher.Coordinators.OutboxCoordinator.Services.Interfaces;
using PollingOutboxPublisher.Database.Repositories.Interfaces;
using PollingOutboxPublisher.Extensions;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Coordinators.OutboxCoordinator.Services;

public class MissingDetector: IMissingDetector
{
    private readonly IMissingEventRepository _missingEventRepository;
    private readonly ILogger<MissingDetector> _logger;

    public MissingDetector(IMissingEventRepository missingEventRepository, ILogger<MissingDetector> logger)
    {
        _missingEventRepository = missingEventRepository;
        _logger = logger;
    }

    public async Task DetectAsync(OutboxEventsBatch outboxEventsBatch)
    {
        var missingIds = FindMissingIds(outboxEventsBatch.Events.ToList(), outboxEventsBatch.LatestOffset);
            
        if (missingIds.IsNullOrEmpty()) return;
            
        _logger.LogInformation("Gap detected. Size: {Size}, MissingIds: {MissingIds}", missingIds.Count,
            string.Join(", ", missingIds));

        var missingEvents = GenerateMissingEvents(missingIds);
        foreach (var missingEvent in missingEvents)
        {
            await _missingEventRepository.InsertAsync(missingEvent);
        }
    }

    private static List<MissingEvent> GenerateMissingEvents(IEnumerable<long> missingIds)
    {
        return missingIds.Select(x => new MissingEvent
            { Id = x, MissedDate = DateTime.UtcNow, RetryCount = 0 }).ToList();
    }

    private static List<long> FindMissingIds(IReadOnlyCollection<OutboxEvent> rows, long? latestOffsetId)
    {
        var missingOffsets = new List<long>();
        var totalRow = rows.Count;
        if (totalRow == 0 || !latestOffsetId.HasValue)
        {
            return missingOffsets;
        }

        var sortedRows = rows.OrderBy(row => row.Id).ToList();
        if (sortedRows[0].Id != latestOffsetId)
        {
            var expectedOffset = latestOffsetId + 1;
            missingOffsets.AddRange(GetMissingEventsBetweenIds(expectedOffset!.Value, sortedRows[0].Id));
        }

        for (var index = 0; index < totalRow - 1; index++)
        {
            var row = sortedRows[index];
            var expectedNextOffset = row.Id + 1;
            var nextOffset = sortedRows[index + 1].Id;
            if (expectedNextOffset != nextOffset)
            {
                missingOffsets.AddRange(GetMissingEventsBetweenIds(expectedNextOffset, nextOffset));
            }
        }

        return missingOffsets;
    }

    private static IEnumerable<long> GetMissingEventsBetweenIds(long firstOffset, long secondOffset)
    {
        var missingOffsets = new List<long>();
        for (var index = firstOffset; index < secondOffset; index++)
        {
            missingOffsets.Add(index);
        }

        return missingOffsets;
    }
}