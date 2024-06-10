using System.Threading.Tasks;
using Confluent.Kafka;

namespace PollingOutboxPublisher.Coordinators.Services.Interfaces;

public interface IKafkaService
{
    Task<DeliveryResult<string, string>> ProduceAsync(string topic, Message<string, string> message);
}