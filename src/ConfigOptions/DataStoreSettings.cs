using System.Diagnostics.CodeAnalysis;

namespace PollingOutboxPublisher.ConfigOptions;

[ExcludeFromCodeCoverage]
public class DataStoreSettings
{
    public string OutboxEvents { get; set; }
    public string MissingEvents { get; set; }
    public string ExceededEvents { get; set; }
    public string OutboxOffset { get; set; }
}