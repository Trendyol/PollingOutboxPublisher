using System.Threading;
using System.Threading.Tasks;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Coordinators.OutboxCoordinator.Services.Interfaces;

public interface IPollingQueue
{
    Task<OutboxEvent[]> DequeueAsync(CancellationToken cancellationToken);
}