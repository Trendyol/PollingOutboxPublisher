using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PollingOutboxPublisher.ConfigOptions;
using PollingOutboxPublisher.Coordinators.OutboxCoordinator;
using PollingOutboxPublisher.Coordinators.OutboxCoordinator.Interfaces;
using PollingOutboxPublisher.Coordinators.OutboxCoordinator.Services;
using PollingOutboxPublisher.Coordinators.OutboxCoordinator.Services.Interfaces;
using PollingOutboxPublisher.Coordinators.Services;
using PollingOutboxPublisher.Coordinators.Services.Interfaces;
using PollingOutboxPublisher.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace PollingOutboxPublisher.Daemon;

[ExcludeFromCodeCoverage]
public class OutboxEventsDaemon : BackgroundService
{
    private readonly ILogger<OutboxEventsDaemon> _logger;
    private readonly IOutboxCoordinator _coordinator;
    private readonly ICircuitBreaker _circuitBreaker;
    private readonly int _waitTimeWhenCircuitBreakerOpenMs;

    public OutboxEventsDaemon(ILogger<OutboxEventsDaemon> logger, IOutboxDispatcher outboxDispatcher,
        IPollingSource pollingSource, IOffsetSetter offsetSetter, IOptions<WorkerSettings> workerSettings, 
        IOptions<DataStoreSettings> tableNameSettings, IOptions<BenchmarkOptions> benchmarkOptions, 
        IMissingDetector missingDetector, IMasterPodChecker masterPodChecker, 
        [FromKeyedServices("OutboxEventsCircuitBreaker")] ICircuitBreaker circuitBreaker)
    {
        _logger = logger;
        _circuitBreaker = circuitBreaker;
        _waitTimeWhenCircuitBreakerOpenMs = workerSettings.Value.RedeliveryDelayAfterError;
        _logger.LogInformation("BenchmarkOptions: {@BenchmarkOptions}", benchmarkOptions.Value);
        _logger.LogInformation("WorkerSettings: {@WorkerSettings}", workerSettings.Value);
        _logger.LogInformation("TableNameSettings: {@TableNameSettings}", tableNameSettings.Value);
            
        var pollingQueue = new PollingOutboxQueue(pollingSource, workerSettings, missingDetector);
        _coordinator = new OutboxCoordinator(outboxDispatcher, pollingQueue, offsetSetter, masterPodChecker);
    }
        

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxEventsDaemon Starting...");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_circuitBreaker.IsOpen)
                {
                    _logger.LogWarning("Circuit breaker is open. Waiting {WaitTime}ms before next attempt in OutboxEventsDaemon.", 
                        _waitTimeWhenCircuitBreakerOpenMs);
                    await Task.Delay(_waitTimeWhenCircuitBreakerOpenMs, stoppingToken);
                    continue;
                }

                await _coordinator.StartAsync(stoppingToken);
                _circuitBreaker.Reset();
            }
            catch (DatabaseOperationException e)
            {
                _logger.LogWarning(e, 
                    "Database operation failed in OutboxEventsDaemon. Circuit breaker state change: Recording failure.");
                _circuitBreaker.RecordFailure();
                await Task.Delay(_waitTimeWhenCircuitBreakerOpenMs, stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e,
                    "Non-database exception thrown in OutboxEventsDaemon. " +
                    "Process should not stopped, exception ignored. " +
                    "Retrying until a result is obtained");
            }
        }
    }
}