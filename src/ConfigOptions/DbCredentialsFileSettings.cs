using System.Diagnostics.CodeAnalysis;

namespace PollingOutboxPublisher.ConfigOptions;

[ExcludeFromCodeCoverage]
public class DbCredentialsFileSettings
{
    public string FileName { get; set; }
    public string Host { get; set; }
    public string Database { get; set; }
    public int Port { get; set; }
    public string ApplicationName { get; set; }
    public string AdditionalParameters { get; set; }
}