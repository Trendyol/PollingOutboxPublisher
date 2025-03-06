using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PollingOutboxPublisher.ConfigOptions;
using PollingOutboxPublisher.Coordinators.MissingCoordinator;
using PollingOutboxPublisher.Coordinators.MissingCoordinator.Interfaces;
using PollingOutboxPublisher.Coordinators.MissingCoordinator.Services;
using PollingOutboxPublisher.Coordinators.MissingCoordinator.Services.Interfaces;
using PollingOutboxPublisher.Coordinators.Services;
using PollingOutboxPublisher.Coordinators.Services.Interfaces;
using PollingOutboxPublisher.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace PollingOutboxPublisher.Daemon;

[ExcludeFromCodeCoverage]
public class MissingEventsDaemon : BackgroundService
{
    private readonly ILogger<MissingEventsDaemon> _logger;
    private readonly IMissingEventsCoordinator _coordinator;
    private readonly ICircuitBreaker _circuitBreaker;
    private readonly int _waitTimeWhenCircuitBreakerOpenMs;

    public MissingEventsDaemon(ILogger<MissingEventsDaemon> logger, IOutboxDispatcher outboxDispatcher,
        IPollingMissingEventsSource pollingMissingEventsSource, IOptions<WorkerSettings> outboxSettings,
        IMissingEventCleaner missingEventCleaner, IMasterPodChecker masterPodChecker, 
        [FromKeyedServices("MissingEventsCircuitBreaker")] ICircuitBreaker circuitBreaker)
    {
        _logger = logger;
        _circuitBreaker = circuitBreaker;
        _waitTimeWhenCircuitBreakerOpenMs = outboxSettings.Value.RedeliveryDelayAfterError;
        var pollingMissingEventsQueue = new PollingMissingEventsQueue(pollingMissingEventsSource, outboxSettings);
        _coordinator =
            new MissingEventsCoordinator(outboxDispatcher, pollingMissingEventsQueue, missingEventCleaner, masterPodChecker, circuitBreaker);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MissingEventsDaemon Starting...");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_circuitBreaker.IsOpen)
                {
                    _logger.LogWarning("Circuit breaker is open. Waiting {WaitTime}ms before next attempt in MissingEventsDaemon.", 
                        _waitTimeWhenCircuitBreakerOpenMs);
                    await Task.Delay(_waitTimeWhenCircuitBreakerOpenMs, stoppingToken);
                    continue;
                }

                await _coordinator.StartAsync(stoppingToken);
            }
            catch (DatabaseOperationException e)
            {
                _logger.LogWarning(e, 
                    "Database operation failed in MissingEventsDaemon. Circuit breaker state change: Recording failure.");
                _circuitBreaker.RecordFailure();
            }
            catch (Exception e)
            {
                _logger.LogError(e,
                    "Non-database exception thrown in MissingEventsDaemon. " +
                    "Process should not stopped, exception ignored. " +
                    "Retrying until a result is obtained");
            }
        }
    }
}