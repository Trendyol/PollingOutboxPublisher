using System.Threading.Tasks;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Database.Repositories.Interfaces;

public interface IExceededEventRepository
{
    Task InsertAsync(ExceededEvent exceededEvent);
}