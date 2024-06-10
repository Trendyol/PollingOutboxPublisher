using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Couchbase.KeyValue;
using Microsoft.Extensions.Options;
using PollingOutboxPublisher.ConfigOptions;
using PollingOutboxPublisher.Database.Providers.Interfaces;
using PollingOutboxPublisher.Database.Repositories.Interfaces;
using PollingOutboxPublisher.Exceptions;

namespace PollingOutboxPublisher.Database.Repositories.Couchbase;

[ExcludeFromCodeCoverage]
public class CouchbaseOutboxOffsetRepository : IOutboxOffsetRepository
{
    private readonly string _tableName;
    private readonly ICouchbaseScopeProvider _couchbaseScopeProvider;
    private ICouchbaseCollection _collection;
    private IScope _scope;

    public CouchbaseOutboxOffsetRepository(ICouchbaseScopeProvider couchbaseScopeProvider,
        IOptions<DataStoreSettings> dataStoreSettings)
    {
        _couchbaseScopeProvider = couchbaseScopeProvider;
        _tableName = dataStoreSettings.Value.OutboxOffset ??
                     throw new MissingConfigurationException(nameof(dataStoreSettings.Value.OutboxOffset));
    }

    private async Task InitializeScopeAsync()
    {
        _scope ??= await _couchbaseScopeProvider.GetScopeAsync();
    }

    private async Task InitializeCollectionAsync()
    {
        await InitializeScopeAsync();
        _collection ??= await _scope.CollectionAsync(_tableName);
    }

    public async Task<long?> GetLatestOffsetAsync()
    {
        await InitializeCollectionAsync();
        var result = await _collection.GetAsync("offset");
        return result.ContentAs<long>();
    }

    public async Task UpdateOffsetAsync(long latestOffSet)
    {
        await InitializeCollectionAsync();
        await _collection.UpsertAsync("offset", latestOffSet);
    }
}