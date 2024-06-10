using System.Diagnostics.CodeAnalysis;

namespace PollingOutboxPublisher.ConfigOptions;

[ExcludeFromCodeCoverage]
public class WorkerSettings
{
    public int OutboxEventsBatchSize { get; set; }
    public int MissingEventsBatchSize { get; set; }
    public int MissingEventsWaitDuration { get; set; }
    public int MissingEventsMaxRetryCount { get; set; }
    public int QueueWaitDuration { get; set; }
    public int BrokerErrorsMaxRetryCount { get; set; }
    public int RedeliveryDelayAfterError { get; set; }
}

// Settings for lagged events, e.g: There are 100.000 events in the MissingOutboxEvents table
// and the first event is missed 6 hours ago. So no need to wait for the publishingPeriod
// (because this db row is missing, it will not be committed)
// and no need for maxRetryCount for the same reason. Also, the batchSize could be bigger.
// Note: Batch size cannot be more than 2000 due to SQL Server IN query.
// Better solutions should be considered (to prevent lagging; e.g: Kafka bulk publish etc.)

// MissingEventsWaitDuration = 1000
// MissingEventsMaxRetryCount = 1
// MissingEventsBatchSize=500

//These are the first settings
// public int OutboxEventsBatchSize { get; set; } = 5000;
// public int MissingEventsBatchSize { get; set; } = 500;
// public int MissingEventsWaitDuration { get; set; } = 20 * 1000;
// public int MissingEventsMaxRetryCount { get; set; } = 2;
// public int QueueWaitDuration { get; set; } = 100;
// public int BrokerErrorsMaxRetryCount { get; set; } = 5;
// public int RedeliveryDelayAfterError { get; set; } = 250;