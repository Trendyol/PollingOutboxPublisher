using System.Threading.Tasks;

namespace PollingOutboxPublisher.Database.Repositories.Interfaces;

public interface IOutboxOffsetRepository
{
    Task<long?> GetLatestOffsetAsync();
    Task UpdateOffsetAsync(long latestOffSet);
}