using System.Diagnostics.CodeAnalysis;

namespace PollingOutboxPublisher.ConfigOptions;

[ExcludeFromCodeCoverage]
public class Couchbase
{
    public string Bucket { get; set; }
    public string Scope { get; set; }
    public string Host { get; set; }
    public string Password { get; set; }
    public string Username { get; set; }
}