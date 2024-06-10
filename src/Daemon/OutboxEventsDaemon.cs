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
using PollingOutboxPublisher.Coordinators.Services.Interfaces;

namespace PollingOutboxPublisher.Daemon;

[ExcludeFromCodeCoverage]
public class OutboxEventsDaemon : BackgroundService
{
    private readonly ILogger<OutboxEventsDaemon> _logger;
    private readonly IOutboxCoordinator _coordinator;

    public OutboxEventsDaemon(ILogger<OutboxEventsDaemon> logger, IOutboxDispatcher outboxDispatcher,
        IPollingSource pollingSource, IOffsetSetter offsetSetter, IOptions<WorkerSettings> workerSettings, IOptions<DataStoreSettings> tableNameSettings,
        IOptions<BenchmarkOptions> benchmarkOptions, IMissingDetector missingDetector, IMasterPodChecker masterPodChecker)
    {
        _logger = logger;
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
                await _coordinator.StartAsync(stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e,
                    "Exception thrown. " +
                    "Process should not stopped, exception ignored. " +
                    "Retrying until a result is obtained");
            }
        }
    }
}