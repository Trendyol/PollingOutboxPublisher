using System.Collections.Generic;
using System.Threading.Tasks;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Database.Repositories.Interfaces;

public interface IMissingEventRepository
{
    Task InsertAsync(MissingEvent missingEvent);
    Task UpdateRetryCountAndExceptionThrownAsync(MissingEvent missingEvent);
    Task<List<MissingEvent>> GetMissingEventsAsync(int batchCount);
    Task IncrementRetryCountAsync(IEnumerable<long> ids);
    Task DeleteMissingEventsAsync(IReadOnlyCollection<long> idList);
}