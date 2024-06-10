using System;
using System.Diagnostics.CodeAnalysis;

namespace PollingOutboxPublisher.Models;

[ExcludeFromCodeCoverage]
public class ExceededEvent
{
    public long Id { get; set; }
    public DateTime MissedDate { get; set; }
    public int RetryCount { get; set; }
    public DateTime ExceededDate { get; set; }
    public bool ExceptionThrown { get; set; }
}