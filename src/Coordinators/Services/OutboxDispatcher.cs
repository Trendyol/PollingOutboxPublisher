using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewRelic.Api.Agent;
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

    [Transaction]
    public async Task DispatchAsync(OutboxEvent outboxEvent)
    {
            try
            {
                await PublishAsync(outboxEvent);
                _logger.LogInformation("Event published. OutboxId: {OutboxId}", outboxEvent.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Message broker is unavailable. Exception: {Exception}", ex.Message);

                //move to missing events with has exception
                await HandleGenericException(outboxEvent);
            }
        }

    [Trace]
    private async Task HandleGenericException(OutboxEvent outboxEvent)
    {
            try
            {
                var missingEvent = outboxEvent.ToMissingEvent();
                missingEvent.ExceptionThrown = true;
                await _missingEventRepository.InsertAsync(missingEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occured when trying to handle generic exception. Exception: {Exception}",
                    ex.Message);
            }
        }

    [Transaction]
    public async Task<MappedMissingEvent> DispatchMissingAsync(MappedMissingEvent mappedMissingEvent)
    {
            try
            {
                await PublishAsync(mappedMissingEvent.OutboxEvent);

                // Set ExceptionThrown as false, and later this will be removed from missing events
                mappedMissingEvent.MissingEvent.ExceptionThrown = false;
                _logger.LogInformation("Event published. OutboxId: {OutboxId}", mappedMissingEvent.OutboxEvent.Id);
            }
            catch (MessageBrokerUnavailableException ex)
            {
                _logger.LogError(ex, "Message broker is unavailable. Exception: {Exception}", ex.Message);
                HandleBrokerUnavailableException(mappedMissingEvent);
            }
            catch (MessageBrokerDeliveryFailedException ex)
            {
                _logger.LogError(ex, "Message broker delivery failed. Exception: {Exception}", ex.Message);
                await HandleDeliveryException(mappedMissingEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred. Exception: {Exception}", ex.Message);
                await HandleGenericExceptionForMissing(mappedMissingEvent);
            }

            return mappedMissingEvent;
        }

    [Trace]
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

    private static void HandleBrokerUnavailableException(MappedMissingEvent mappedMissingEvent)
    {
            mappedMissingEvent.MissingEvent.ExceptionThrown = true;
        }

    [Trace]
    private async Task HandleDeliveryException(MappedMissingEvent mappedMissingEvent)
    {
            try
            {
                if (mappedMissingEvent.MissingEvent.IsRetryCountLessThan(_kafkaErrorsMaxRetryCount))
                {
                    mappedMissingEvent.MissingEvent.RetryCount += 1;
                    mappedMissingEvent.MissingEvent.ExceptionThrown = true;
                    await _missingEventRepository.UpdateRetryCountAndExceptionThrownAsync(mappedMissingEvent
                        .MissingEvent);
                    await Task.Delay(_redeliveryDelayAfterError);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occured when trying to handle delivery exception. Exception: {Exception}",
                    ex.Message);
            }
        }

    [Trace]
    private async Task HandleGenericExceptionForMissing(MappedMissingEvent mappedMissingEvent)
    {
            try
            {
                if (mappedMissingEvent.MissingEvent.IsRetryCountLessThan(_kafkaErrorsMaxRetryCount))
                {
                    mappedMissingEvent.MissingEvent.RetryCount += 1;
                    mappedMissingEvent.MissingEvent.ExceptionThrown = true;
                    await _missingEventRepository.UpdateRetryCountAndExceptionThrownAsync(mappedMissingEvent
                        .MissingEvent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occured when trying to handle generic exception. Exception: {Exception}",
                    ex.Message);
            }
        }
}