using System.Threading;
using System.Threading.Tasks;

namespace PollingOutboxPublisher.Coordinators.MissingCoordinator.Interfaces;

public interface IMissingEventsCoordinator
{
    Task StartAsync(CancellationToken cancellationToken);
}