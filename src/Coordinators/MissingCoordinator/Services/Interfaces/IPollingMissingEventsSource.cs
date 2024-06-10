using System.Threading.Tasks;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Coordinators.MissingCoordinator.Services.Interfaces;

public interface IPollingMissingEventsSource
{
    Task<MissingEvent[]> GetMissingEventsAsync(int batchCount);
    Task<OutboxEvent[]> GetNextOutboxEventsAsync(MissingEvent[] retryableMissingEvents);
}