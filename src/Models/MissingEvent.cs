using System;
using System.Diagnostics.CodeAnalysis;

namespace PollingOutboxPublisher.Models;

[ExcludeFromCodeCoverage]
public class MissingEvent
{
    public long Id { get; set; }
    public DateTime MissedDate { get; set; }
    public int RetryCount { get; set; }
    public bool ExceptionThrown { get; set; }

    public bool IsRetryCountLessThan(int than)
    {
        return RetryCount < than;
    }

    public ExceededEvent ToExceedEvent() => new()
    {
        Id = Id, RetryCount = RetryCount, MissedDate = MissedDate, ExceededDate = DateTime.UtcNow,
        ExceptionThrown = ExceptionThrown
    };
}