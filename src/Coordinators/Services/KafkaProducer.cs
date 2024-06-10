using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using NewRelic.Api.Agent;
using PollingOutboxPublisher.ConfigOptions;
using PollingOutboxPublisher.Coordinators.Services.Interfaces;

namespace PollingOutboxPublisher.Coordinators.Services;

[ExcludeFromCodeCoverage]
public sealed class KafkaProducer : IKafkaProducer
{
    private IProducer<string, string> _producer;

    public KafkaProducer(IOptions<Kafka> kafkaOptions)
    {
        var kafkaConfiguration = kafkaOptions.Value;
        BuildProducer(kafkaConfiguration);
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
            SaslMechanism = kafka.SaslMechanism ?? SaslMechanism.ScramSha512,
            SecurityProtocol = kafka.SecurityProtocol ?? SecurityProtocol.SaslSsl,
            BatchSize = kafka.BatchSize ?? 512 * 1024,  // 30000000
            LingerMs = kafka.LingerMs ?? 10,
            CompressionType = kafka.CompressionType ?? CompressionType.Snappy,
            MessageMaxBytes = kafka.MessageMaxBytes ?? 30000000,
            Acks = kafka.Acks ?? Acks.Leader
        };
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