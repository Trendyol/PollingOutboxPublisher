using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PollingOutboxPublisher.ConfigOptions;
using PollingOutboxPublisher.Coordinators.MissingCoordinator.Services.Interfaces;
using PollingOutboxPublisher.Database.Repositories.Interfaces;
using PollingOutboxPublisher.Extensions;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Coordinators.MissingCoordinator.Services;

public class MissingEventCleaner : IMissingEventCleaner
{
    private readonly ILogger<MissingEventCleaner> _logger;
    private readonly IMissingEventRepository _missingEventRepository;
    private readonly IExceededEventRepository _exceededEventRepository;
    private readonly int _missingEventsMaxRetryCount;
    private readonly int _brokerErrorsMaxRetryCount;

    public MissingEventCleaner(IOptions<WorkerSettings> workerSettings, ILogger<MissingEventCleaner> logger,
        IMissingEventRepository missingEventRepository, IExceededEventRepository exceededEventRepository)
    {
        _logger = logger;
        _missingEventRepository = missingEventRepository;
        _exceededEventRepository = exceededEventRepository;
        _missingEventsMaxRetryCount = workerSettings.Value.MissingEventsMaxRetryCount;
        _brokerErrorsMaxRetryCount = workerSettings.Value.BrokerErrorsMaxRetryCount;
    }

    public async Task CleanMissingEventsHaveOutboxEventAsync(MissingEvent[] missingEvents,
        List<MappedMissingEvent> results)
    {
        await CleanPublishedMissingEventsAsync(results);

        // after publishing, missingEvents retry count may change.
        // but with this change retry count hold both for both missing outbox item process count and publishing exception count
        // example: RetryCount = 5, 3 - fail count for getting outbox item from table, 2 - fail count when publishing
        var updatedMissingEvents = results.Select(x => x.MissingEvent).ToList();
        UpdateListElements(missingEvents.ToList(), updatedMissingEvents);

        await PushToExceeded(missingEvents, _brokerErrorsMaxRetryCount);
    }

    public async Task CleanMissingEventsNotHaveOutboxEventAsync(MissingEvent[] missingEvents)
    {
        await PushToExceeded(missingEvents, _missingEventsMaxRetryCount);
    }

    public async Task HandleNonMatchedMissingEventsAsync(OutboxEvent[] outboxEvents,
        MissingEvent[] retryableMissingEvents)
    {
        if (retryableMissingEvents is null) return;
        outboxEvents ??= Array.Empty<OutboxEvent>();
        var outboxEventIds = outboxEvents.Select(x => x.Id).ToArray();
        var retryableMissingEventsIds = retryableMissingEvents.Select(x => x.Id).ToArray();

        var missedRetriedEvents = retryableMissingEventsIds.Except(outboxEventIds).ToList();
        if (!missedRetriedEvents.IsNullOrEmpty())
        {
            await _missingEventRepository.IncrementRetryCountAsync(missedRetriedEvents);
        }
    }

    private async Task CleanPublishedMissingEventsAsync(IEnumerable<MappedMissingEvent> results)
    {
        // remove only doesn't have exception, because means that they sent 
        var missingEventDontHaveExceptionIdList = results.Where(x => !x.MissingEvent.ExceptionThrown)
            .Select(x => x.MissingEvent.Id).ToList();
        if (!missingEventDontHaveExceptionIdList.IsNullOrEmpty())
        {
            _logger.LogInformation(
                "Missing events are discarded since they published successfully. PublishedMissingEvents: {@PublishedMissingEvents}",
                missingEventDontHaveExceptionIdList);
            await _missingEventRepository.DeleteMissingEventsAsync(missingEventDontHaveExceptionIdList);
        }
    }

    private async Task PushToExceeded(IEnumerable<MissingEvent> missingEvents, int retryLimit)
    {
        var retryLimitExceededMissingEvents = missingEvents.Where(x => x.RetryCount >= retryLimit).ToList();
        if (retryLimitExceededMissingEvents.IsNullOrEmpty()) return;

        _logger.LogInformation(
            "Missing events are discarded since retry limit is exceed. Database transactions need to be investigated. Count: {Count}",
            retryLimitExceededMissingEvents.Count);

        var exceededEvents = retryLimitExceededMissingEvents.Select(x => x.ToExceedEvent()).ToList();
        
        foreach (var exceededEvent in exceededEvents)
        {
            await _exceededEventRepository.InsertAsync(exceededEvent);
        }

        var retryLimitExceededMissingEventIds = retryLimitExceededMissingEvents.Select(x => x.Id).ToList();
        await _missingEventRepository.DeleteMissingEventsAsync(retryLimitExceededMissingEventIds);
    }


    private static void UpdateListElements(List<MissingEvent> listToUpdate, List<MissingEvent> updateValues)
    {
        foreach (var updatedItem in updateValues)
        {
            var existingItemIndex = listToUpdate.FindIndex(item => item.Id == updatedItem.Id);
            if (existingItemIndex != -1)
            {
                listToUpdate[existingItemIndex] = updatedItem;
            }
            else
            {
                listToUpdate.Add(updatedItem);
            }
        }
    }
}