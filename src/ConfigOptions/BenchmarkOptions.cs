using System.Diagnostics.CodeAnalysis;

namespace PollingOutboxPublisher.ConfigOptions;

[ExcludeFromCodeCoverage]
public class BenchmarkOptions
{
    public bool IsPublishingOn { get; set; }
}