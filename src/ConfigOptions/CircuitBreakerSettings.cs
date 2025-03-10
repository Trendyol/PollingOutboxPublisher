using System.Diagnostics.CodeAnalysis;

namespace PollingOutboxPublisher.ConfigOptions;

[ExcludeFromCodeCoverage]
public class CircuitBreakerSettings
{
    public bool IsEnabled { get; set; } = false;
    public int Threshold { get; set; } = 3;
    public int DurationSc { get; set; } = 600;
    public int HalfOpenMaxAttempts { get; set; } = 1;
} 