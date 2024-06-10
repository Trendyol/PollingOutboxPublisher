using System;
using System.Diagnostics.CodeAnalysis;
using PollingOutboxPublisher.Models.Interfaces;

namespace PollingOutboxPublisher.Models;

[ExcludeFromCodeCoverage]
public class OutboxEvent : IMessage
{
    public long Id { get; set; }
    public string Key { get; set; }
    public string Value { get; set; }
    public string Topic { get; set; }
    public string Header { get; set; }

    public MissingEvent ToMissingEvent() => new() { Id = Id, MissedDate = DateTime.UtcNow };
}