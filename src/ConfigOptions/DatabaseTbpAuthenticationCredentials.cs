using System.Diagnostics.CodeAnalysis;

namespace PollingOutboxPublisher.ConfigOptions;

[ExcludeFromCodeCoverage]
public class DatabaseTbpAuthenticationCredentials
{
    public string ClusterName { get; set; }
    public string Host { get; set; }
    public string Database { get; set; }
    public int Port { get; set; }
    public string ApplicationName { get; set; }
}