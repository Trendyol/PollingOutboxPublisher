using System.Diagnostics.CodeAnalysis;
using Confluent.Kafka;

namespace PollingOutboxPublisher.ConfigOptions;

[ExcludeFromCodeCoverage]
public class Kafka
{
    public string SaslUsername { get; set; }
    public string Brokers { get; set; }
    public string SslCaLocation { get; set; }
    public string SaslPassword { get; set; }
    public string SslKeystorePassword { get; set; }
    public SaslMechanism? SaslMechanism { get; set; }
    public SecurityProtocol? SecurityProtocol { get; set; }
    public int? BatchSize { get; set; }
    public CompressionType? CompressionType { get; set; }
    public int? MessageMaxBytes { get; set; }
    public Acks? Acks { get; set; }
    public double? LingerMs { get; set; }
    public string ClientId { get; set; }
}