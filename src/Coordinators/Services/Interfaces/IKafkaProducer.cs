using System;
using System.Threading.Tasks;
using Confluent.Kafka;

namespace PollingOutboxPublisher.Coordinators.Services.Interfaces;

public interface IKafkaProducer : IDisposable
{
    Task<DeliveryResult<string, string>> ProduceAsync(string topic, Message<string, string> message);
}