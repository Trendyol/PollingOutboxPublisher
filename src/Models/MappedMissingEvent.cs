using System.Diagnostics.CodeAnalysis;

namespace PollingOutboxPublisher.Models;

[ExcludeFromCodeCoverage]
public class MappedMissingEvent
{
    public MissingEvent MissingEvent { get; set; }
    public OutboxEvent OutboxEvent { get; set; }
}