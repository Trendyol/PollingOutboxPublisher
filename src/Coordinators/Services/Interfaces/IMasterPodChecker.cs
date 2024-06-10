using System.Threading;
using System.Threading.Tasks;

namespace PollingOutboxPublisher.Coordinators.Services.Interfaces;

public interface IMasterPodChecker
{
    Task TryToBecomeMasterPodAsync(CancellationToken cancellationToken);
    Task<bool> IsMasterPodAsync(CancellationToken cancellationToken);
}