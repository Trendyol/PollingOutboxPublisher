using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PollingOutboxPublisher.ConfigOptions;
using PollingOutboxPublisher.Coordinators.Services.Interfaces;
using PollingOutboxPublisher.Database.Repositories.Interfaces;
using PollingOutboxPublisher.Exceptions;
using PollingOutboxPublisher.Helper;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Coordinators.Services;

public class OutboxDispatcher : IOutboxDispatcher
{
    private readonly IOptions<BenchmarkOptions> _benchmarkOptions;
    private readonly IKafkaService _kafkaService;
    private readonly ILogger<OutboxDispatcher> _logger;
    private readonly IMissingEventRepository _missingEventRepository;
    private readonly int _kafkaErrorsMaxRetryCount;
    private readonly int _redeliveryDelayAfterError;

    public OutboxDispatcher(ILogger<OutboxDispatcher> logger,
        IKafkaService kafkaService, IOptions<BenchmarkOptions> benchmarkOptions,
        IOptions<WorkerSettings> workerSettings,
        IMissingEventRepository missingEventRepository)
    {
        _logger = logger;
        _kafkaService = kafkaService;
        _benchmarkOptions = benchmarkOptions;
        _missingEventRepository = missingEventRepository;
        _kafkaErrorsMaxRetryCount = workerSettings.Value.BrokerErrorsMaxRetryCount;
        _redeliveryDelayAfterError = workerSettings.Value.RedeliveryDelayAfterError;
    }

    public async Task DispatchAsync(OutboxEvent outboxEvent)
    {
        try
        {
            await PublishAsync(outboxEvent);
            _logger.LogInformation("Event published. OutboxId: {OutboxId}", outboxEvent.Id);
        }
        catch (Exception ex)
        {
            await LogErrorAndHandle(ex, "Message broker is unavailable",
                () =>  HandleGenericException(outboxEvent));
        }
    }

    public async Task<MappedMissingEvent> DispatchMissingAsync(MappedMissingEvent mappedMissingEvent)
    {
        try
        {
            await PublishAsync(mappedMissingEvent.OutboxEvent);
            mappedMissingEvent.MissingEvent.ExceptionThrown = false;
            _logger.LogInformation("Event published. OutboxId: {OutboxId}", mappedMissingEvent.OutboxEvent.Id);
        }
        catch (MessageBrokerUnavailableException ex)
        {
            await LogErrorAndHandle(ex, "Message broker is unavailable",
                () => HandleBrokerUnavailableException(mappedMissingEvent));
        }
        catch (MessageBrokerDeliveryFailedException ex)
        {
            await LogErrorAndHandle(ex, "Message broker delivery failed",
                () => UpdateMissingEventRetryCount(mappedMissingEvent, true));
        }
        catch (Exception ex)
        {
            await LogErrorAndHandle(ex, "An error occurred",
                () => UpdateMissingEventRetryCount(mappedMissingEvent, false));
        }

        return mappedMissingEvent;
    }

    private async Task PublishAsync(OutboxEvent outboxEvent)
    {
        if (!_benchmarkOptions.Value.IsPublishingOn)
        {
            _logger.LogInformation("Publishing closed. IsPublishingOn: {IsPublishingOn}",
                _benchmarkOptions.Value.IsPublishingOn);
            return;
        }

        var kafkaModel = outboxEvent.PrepareKafkaMessageModel(outboxEvent.Topic, outboxEvent.Key);
        await _kafkaService.ProduceAsync(kafkaModel.TopicName, kafkaModel.Message);
    }

    private async Task HandleGenericException(OutboxEvent outboxEvent)
    {
        var missingEvent = outboxEvent.ToMissingEvent();
        missingEvent.ExceptionThrown = true;
        await _missingEventRepository.InsertAsync(missingEvent);
    }

    private static Task HandleBrokerUnavailableException(MappedMissingEvent mappedMissingEvent)
    {
        mappedMissingEvent.MissingEvent.ExceptionThrown = true;
        return Task.CompletedTask;
    }

    private async Task UpdateMissingEventRetryCount(MappedMissingEvent mappedMissingEvent, bool shouldDelay)
    {
        if (!mappedMissingEvent.MissingEvent.IsRetryCountLessThan(_kafkaErrorsMaxRetryCount))
            return;

        try
        {
            mappedMissingEvent.MissingEvent.RetryCount += 1;
            mappedMissingEvent.MissingEvent.ExceptionThrown = true;
            await _missingEventRepository.UpdateRetryCountAndExceptionThrownAsync(mappedMissingEvent.MissingEvent);

            if (shouldDelay)
                await Task.Delay(_redeliveryDelayAfterError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update missing event retry count in database");
            throw new DatabaseOperationException("Failed to update missing event retry count", ex);
        }
    }

    private async Task LogAndRethrowException(Func<Task> handler)
    {
        try
        {
            await handler();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while handling exception: {Exception}", ex.Message);
            throw;
        }
    }

    private async Task LogErrorAndHandle(Exception ex, string message, Func<Task> handler)
    {
        _logger.LogError(ex, "{message}. Exception: {Exception}", message, ex.Message);
        await LogAndRethrowException(handler);
    }
}