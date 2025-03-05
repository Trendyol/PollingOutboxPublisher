namespace PollingOutboxPublisher.Coordinators.Services;

public interface ICircuitBreaker
{
    bool IsOpen { get; }
    void RecordFailure();
    void Reset();
}