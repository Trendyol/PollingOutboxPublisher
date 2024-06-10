using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PollingOutboxPublisher.Redis.Interfaces;
using StackExchange.Redis;

namespace PollingOutboxPublisher.Redis;

public class RedisLockManager : IRedisLockManager
{
    private readonly ILogger<RedisLockManager> _logger;

    private readonly IDatabase _redisDatabase;

    public RedisLockManager(ILogger<RedisLockManager> logger, IRedisClientFactory redisCacheClient)
    {
        _logger = logger;
        _redisDatabase = redisCacheClient.Connect().GetDatabase();
    }

    public async Task<bool> LockTakeAsync(string cacheName, string key, string value, TimeSpan expiration,
        CommandFlags commandFlags = CommandFlags.None)
    {
        bool flag;

        try
        {
            flag = await _redisDatabase.LockTakeAsync($"{cacheName}:{key}", JsonConvert.SerializeObject(value),
                expiration, commandFlags);
        }
        catch (Exception ex)
        {
            _logger.LogError("Acquire lock fail...{ExceptionMessage}, CacheName: {CacheName}, Key: {Key}", ex.Message,
                cacheName, key);
            flag = false;
        }

        return flag;
    }

    public async Task<bool> LockExtendAsync(string cacheName, string key, string value, TimeSpan expiration,
        CommandFlags commandFlags = CommandFlags.None)
    {
        bool flag;

        try
        {
            flag = await _redisDatabase.LockExtendAsync($"{cacheName}:{key}", JsonConvert.SerializeObject(value),
                expiration, commandFlags);
        }
        catch (Exception ex)
        {
            _logger.LogError("Acquire lock fail...{ExceptionMessage}, CacheName: {CacheName}, Key: {Key}", ex.Message,
                cacheName, key);
            flag = false;
        }

        return flag;
    }
}