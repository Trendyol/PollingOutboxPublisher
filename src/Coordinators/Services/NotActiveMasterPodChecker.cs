using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using PollingOutboxPublisher.Coordinators.Services.Interfaces;

namespace PollingOutboxPublisher.Coordinators.Services;

[ExcludeFromCodeCoverage]
public class NotActiveMasterPodChecker : IMasterPodChecker
{
    public Task TryToBecomeMasterPodAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task<bool> IsMasterPodAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }
}