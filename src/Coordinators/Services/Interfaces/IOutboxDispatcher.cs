using System.Threading.Tasks;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Coordinators.Services.Interfaces;

public interface IOutboxDispatcher
{
    Task DispatchAsync(OutboxEvent outboxEvent);
    Task<MappedMissingEvent> DispatchMissingAsync(MappedMissingEvent mappedMissingEvent);
}