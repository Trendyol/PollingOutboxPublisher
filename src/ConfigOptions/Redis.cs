namespace PollingOutboxPublisher.ConfigOptions;

public class Redis
{
    public string Endpoints { get; set; }
    public string Password { get; set; }
    public string Config { get; set; }
    public int DefaultDatabase { get; set; }
}