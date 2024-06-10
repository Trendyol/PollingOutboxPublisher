using System;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace PollingOutboxPublisher.Redis.Interfaces;

public interface IRedisLockManager
{
    Task<bool> LockTakeAsync(string cacheName, string key, string token, TimeSpan expiration, CommandFlags commandFlags = CommandFlags.None);
    Task<bool> LockExtendAsync(string cacheName, string key, string value, TimeSpan expiration, CommandFlags commandFlags = CommandFlags.None);
}