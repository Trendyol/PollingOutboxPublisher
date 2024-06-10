using System.Diagnostics.CodeAnalysis;
using Confluent.Kafka;

namespace PollingOutboxPublisher.Models;

[ExcludeFromCodeCoverage]
public class KafkaMessageModel
{
    public Message<string, string> Message { get; set; }
    public string TopicName { get; set; }
}