using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewRelic.Api.Agent;
using PollingOutboxPublisher.ConfigOptions;
using PollingOutboxPublisher.Coordinators.Services.Interfaces;

namespace PollingOutboxPublisher.Coordinators.Services;

[ExcludeFromCodeCoverage]
public sealed class KafkaProducer : IKafkaProducer
{
    private IProducer<string, string> _producer;
    private Kafka _kafkaConfiguration;
    private readonly ILogger<KafkaProducer> _logger;

    public KafkaProducer(IOptionsMonitor<Kafka> kafkaOptionsMonitor, ILogger<KafkaProducer> logger)
    {
        _logger = logger;
        _kafkaConfiguration = kafkaOptionsMonitor.CurrentValue;
        kafkaOptionsMonitor.OnChange(OnKafkaConfigChanged);
        BuildProducer(_kafkaConfiguration);
    }

    private void OnKafkaConfigChanged(Kafka newKafkaConfig)
    {
        if (!newKafkaConfig.ReloadOnChange) return;
        
        _logger.LogInformation("Kafka configuration changed");
        _kafkaConfiguration = newKafkaConfig;
        BuildProducer(_kafkaConfiguration);
    }
    
    private void BuildProducer(Kafka kafka)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = kafka.Brokers,
            ClientId = kafka.ClientId,
            SaslUsername = kafka.SaslUsername,
            SaslPassword = kafka.SaslPassword,
            SslCaLocation = kafka.SslCaLocation,
            SslKeystorePassword = kafka.SslKeystorePassword,
            SaslMechanism = kafka.SaslMechanism,
            SecurityProtocol = kafka.SecurityProtocol,
            BatchSize = kafka.BatchSize ?? 512 * 1024,  // 30000000
            LingerMs = kafka.LingerMs ?? 10,
            CompressionType = kafka.CompressionType ?? CompressionType.Snappy,
            MessageMaxBytes = kafka.MessageMaxBytes ?? 30000000,
            Acks = kafka.Acks ?? Acks.Leader
        };
        
        if (_producer != null)
        {
            _producer.Flush(TimeSpan.FromSeconds(10));
            _producer.Dispose();
        }
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    [Trace]
    public async Task<DeliveryResult<string, string>> ProduceAsync(string topic, Message<string, string> message)
    {
        var deliveryResult = await _producer.ProduceAsync(topic, message);
        return deliveryResult;
    }

    [Trace]
    public void Dispose()
    {
        _producer?.Flush(TimeSpan.FromSeconds(10));
        _producer?.Dispose();
    }
}