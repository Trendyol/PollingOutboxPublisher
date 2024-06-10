using System.Collections.Generic;
using System.Threading.Tasks;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Database.Repositories.Interfaces;

public interface IOutboxEventRepository
{
    Task<List<OutboxEvent>> GetOutboxEventsAsync(IEnumerable<long> ids);
    Task<long> GetNewestEventIdAsync(long latestOffset);
    Task<List<OutboxEvent>> GetLatestEventsAsync(int batchCount, long newestEventId);
}