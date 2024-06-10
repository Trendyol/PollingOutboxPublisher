using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Coordinators.MissingCoordinator.Services.Interfaces;

public interface IPollingMissingQueue
{
    Task<(MissingEvent[], MissingEvent[], List<MappedMissingEvent>, OutboxEvent[])> DequeueAsync(
        CancellationToken cancellationToken);
}