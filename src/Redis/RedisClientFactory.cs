using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PollingOutboxPublisher.Exceptions;
using PollingOutboxPublisher.Redis.Interfaces;
using StackExchange.Redis;

namespace PollingOutboxPublisher.Redis;

[ExcludeFromCodeCoverage]
public class RedisClientFactory : IRedisClientFactory
{
    private readonly ILogger<RedisClientFactory> _logger;
    private IConnectionMultiplexer _connectionMultiplexer;
    private readonly ConfigOptions.Redis _redis;

    public RedisClientFactory(IOptions<ConfigOptions.Redis> redisConfiguration, ILogger<RedisClientFactory> logger)
    {
        _redis = redisConfiguration.Value;
        _logger = logger;
    }

    public IConnectionMultiplexer Connect()
    {
        if (_connectionMultiplexer is null)
        {
            InitializeConnectionMultiplexer();
        }

        return _connectionMultiplexer;
    }

    private void InitializeConnectionMultiplexer()
    {
        if (_redis.Endpoints is null || 
            _redis.Password is null || 
            _redis.Config is null)
        {
            throw new MissingConfigurationException("RedisConfiguration.Endpoints, RedisConfiguration.Password, RedisConfiguration.Config");
        }
        
        var conf = string.Join(",",
            new List<string>
            {
                _redis.Endpoints, $"password={_redis.Password}", _redis.Config,
                $"defaultDatabase={_redis.DefaultDatabase}"
            });

        _connectionMultiplexer = ConnectionMultiplexer.Connect(conf);
        _logger.LogInformation("Successfully connected to Redis");
    }
}