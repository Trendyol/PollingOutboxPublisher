using System.Diagnostics.CodeAnalysis;

namespace PollingOutboxPublisher.ConfigOptions;

[ExcludeFromCodeCoverage]
public class MasterPodSettings
{
    public string CacheName { get; set; }
    public int MasterPodLifetime { get; set; }
    public int MasterPodRaceInterval { get; set; }
    public int IsMasterPodCheckInterval { get; set; }
}