using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PollingOutboxPublisher.ConfigOptions;
using PollingOutboxPublisher.Coordinators.Services.Interfaces;
using PollingOutboxPublisher.Redis.Interfaces;

namespace PollingOutboxPublisher.Coordinators.Services;

public class MasterPodChecker : IMasterPodChecker
{
    private readonly string _cacheName;
    private const string Key = "MasterPodLock";
    private readonly IRedisLockManager _lockManager;
    private readonly ILogger<MasterPodChecker> _logger;
    private readonly MasterPodSettings _masterPodSettings;
    private readonly string _token;
    private bool _hasLock;

    public MasterPodChecker(ILogger<MasterPodChecker> logger, IOptions<MasterPodSettings> masterPodSettings,
        IRedisLockManager lockManager)
    {
        _logger = logger;
        _masterPodSettings = masterPodSettings.Value;
        _logger.LogInformation("MasterPodSettings: {@MasterPodSettings}", masterPodSettings.Value);
        _lockManager = lockManager;
        _token = Environment.GetEnvironmentVariable("HOSTNAME") ?? Guid.NewGuid().ToString();
        _logger.LogInformation("LockToken: {LockToken}", _token);
        _cacheName = _masterPodSettings.CacheName;
    }

    public async Task TryToBecomeMasterPodAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("TryTakeLeaderLock Starting...");

        if (_hasLock)
            await LockExtendAsync();
        else
            await LockTakeAsync();

        LogPodStatus();

        await Task.Delay(_masterPodSettings.MasterPodRaceInterval, cancellationToken);
    }

    private void LogPodStatus()
    {
        if (_hasLock)
            _logger.LogInformation("Master Pod: {MasterPod}", _token);
        else
            _logger.LogInformation("Follower Pod: {FollowerPod}", _token);
    }

    private async Task LockExtendAsync()
    {
        //using LockExtend is same as get cache, compare token and take TakeLock process  
        _hasLock = await _lockManager.LockExtendAsync(_cacheName, Key, _token, GetMasterPodLifetimeSpan());
        _logger.LogInformation("Try to LockExtend. LockExtendResult: {LockExtendResult}", _hasLock);
    }

    private async Task LockTakeAsync()
    {
        _hasLock = await _lockManager.LockTakeAsync(_cacheName, Key, _token, GetMasterPodLifetimeSpan());
        _logger.LogInformation("Try to LockTake. LockTakeResult: {LockTakeResult}", _hasLock);
    }

    private TimeSpan GetMasterPodLifetimeSpan()
    {
        return TimeSpan.FromMilliseconds(_masterPodSettings.MasterPodLifetime);
    }

    public async Task<bool> IsMasterPodAsync(CancellationToken cancellationToken)
    {
        //if _hasLock is false, wait some duration to do not cause high cpu usage
        if (!_hasLock) await Task.Delay(_masterPodSettings.IsMasterPodCheckInterval, cancellationToken);
        return _hasLock;
    }
}