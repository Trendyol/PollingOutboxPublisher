using System.Threading.Tasks;
using Couchbase.KeyValue;

namespace PollingOutboxPublisher.Database.Providers.Interfaces;

public interface ICouchbaseScopeProvider
{
    Task<IScope> GetScopeAsync();
}