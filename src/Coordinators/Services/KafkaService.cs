using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Confluent.Kafka;
using NewRelic.Api.Agent;
using PollingOutboxPublisher.Coordinators.Services.Interfaces;

namespace PollingOutboxPublisher.Coordinators.Services;

[ExcludeFromCodeCoverage]
public class KafkaService : IKafkaService
{
    private readonly IKafkaProducer _kafkaProducer;

    public KafkaService(IKafkaProducer kafkaProducer)
    {
            _kafkaProducer = kafkaProducer;
        }

    [Trace]
    public Task<DeliveryResult<string, string>> ProduceAsync(string topic, Message<string, string> message)
    {
            return _kafkaProducer.ProduceAsync(topic, message);
        }
}