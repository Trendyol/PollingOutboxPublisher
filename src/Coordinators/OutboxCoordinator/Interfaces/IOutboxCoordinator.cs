using System.Threading;
using System.Threading.Tasks;

namespace PollingOutboxPublisher.Coordinators.OutboxCoordinator.Interfaces;

public interface IOutboxCoordinator
{
    Task StartAsync(CancellationToken cancellationToken);
}