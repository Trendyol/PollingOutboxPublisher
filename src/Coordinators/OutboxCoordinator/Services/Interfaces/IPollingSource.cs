using System.Threading.Tasks;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Coordinators.OutboxCoordinator.Services.Interfaces;

public interface IPollingSource
{
    Task<OutboxEventsBatch> GetNextAsync(int batchCount);
}