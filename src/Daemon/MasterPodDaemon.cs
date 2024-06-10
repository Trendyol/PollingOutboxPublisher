using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PollingOutboxPublisher.Coordinators.Services.Interfaces;

namespace PollingOutboxPublisher.Daemon;

[ExcludeFromCodeCoverage]
public class MasterPodDaemon : BackgroundService
{
    private readonly ILogger<MasterPodDaemon> _logger;
    private readonly IMasterPodChecker _masterPodChecker;

    public MasterPodDaemon(ILogger<MasterPodDaemon> logger, IMasterPodChecker masterPodChecker)
    {
        _logger = logger;
        _masterPodChecker = masterPodChecker;
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LeaderLockCheckerDaemon Starting...");
            
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _masterPodChecker.TryToBecomeMasterPodAsync(stoppingToken);
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