using StackExchange.Redis;

namespace PollingOutboxPublisher.Redis.Interfaces;

public interface IRedisClientFactory
{
    IConnectionMultiplexer Connect();
}