using System;
using System.Diagnostics.CodeAnalysis;

namespace PollingOutboxPublisher.Models;

[ExcludeFromCodeCoverage]
public class OutboxEventsBatch
{
    public OutboxEvent[] Events { get; set; } = Array.Empty<OutboxEvent>();
    public long? LatestOffset { get; set; }
}