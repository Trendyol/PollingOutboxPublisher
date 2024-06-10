using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Couchbase.KeyValue;
using Microsoft.Extensions.Options;
using PollingOutboxPublisher.ConfigOptions;
using PollingOutboxPublisher.Database.Providers.Interfaces;
using PollingOutboxPublisher.Database.Repositories.Interfaces;
using PollingOutboxPublisher.Exceptions;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Database.Repositories.Couchbase;

[ExcludeFromCodeCoverage]
public class CouchbaseExceededEventRepository : IExceededEventRepository
{
    private readonly ICouchbaseScopeProvider _couchbaseScopeProvider;
    private readonly string _tableName;
    private IScope _scope;
    private ICouchbaseCollection _collection;

    public CouchbaseExceededEventRepository(ICouchbaseScopeProvider couchbaseScopeProvider,
        IOptions<DataStoreSettings> dataStoreSettings)
    {
        _couchbaseScopeProvider = couchbaseScopeProvider;
        _tableName = dataStoreSettings.Value.ExceededEvents ??
                     throw new MissingConfigurationException(nameof(dataStoreSettings.Value
                         .ExceededEvents));
    }

    private async Task InitializeCollectionAsync()
    {
        _scope ??= await _couchbaseScopeProvider.GetScopeAsync();
        _collection ??= await _scope.CollectionAsync(_tableName);
    }

    public async Task InsertAsync(ExceededEvent exceededEvent)
    {
        await InitializeCollectionAsync();
        await _collection.UpsertAsync(exceededEvent.Id.ToString(), exceededEvent);
    }
}