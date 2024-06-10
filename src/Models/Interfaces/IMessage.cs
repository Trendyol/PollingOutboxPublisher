namespace PollingOutboxPublisher.Models.Interfaces;

public interface IMessage
{
    string Value { get; set; }
    string Topic { get; set; }
    string Key { get; set; }
    string Header { get; set; }
}