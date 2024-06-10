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
using PollingOutboxPublisher.Coordinators.Services.Interfaces;

namespace PollingOutboxPublisher.Daemon;

[ExcludeFromCodeCoverage]
public class MissingEventsDaemon : BackgroundService
{
    private readonly ILogger<MissingEventsDaemon> _logger;
    private readonly IMissingEventsCoordinator _coordinator;

    public MissingEventsDaemon(ILogger<MissingEventsDaemon> logger, IOutboxDispatcher outboxDispatcher,
        IPollingMissingEventsSource pollingMissingEventsSource, IOptions<WorkerSettings> outboxSettings,
        IMissingEventCleaner missingEventCleaner, IMasterPodChecker masterPodChecker)
    {
        _logger = logger;
        var pollingMissingEventsQueue = new PollingMissingEventsQueue(pollingMissingEventsSource, outboxSettings);
        _coordinator =
            new MissingEventsCoordinator(outboxDispatcher, pollingMissingEventsQueue, missingEventCleaner, masterPodChecker);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MissingEventsDaemon Starting...");
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