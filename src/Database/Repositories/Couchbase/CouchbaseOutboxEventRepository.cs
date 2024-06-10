using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Couchbase;
using Couchbase.KeyValue;
using Couchbase.Query;
using Microsoft.Extensions.Options;
using PollingOutboxPublisher.ConfigOptions;
using PollingOutboxPublisher.Database.Providers.Interfaces;
using PollingOutboxPublisher.Database.Repositories.Interfaces;
using PollingOutboxPublisher.Exceptions;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Database.Repositories.Couchbase;

[ExcludeFromCodeCoverage]
public class CouchbaseOutboxEventRepository : IOutboxEventRepository
{
    private readonly string _tableName;
    private readonly ICouchbaseScopeProvider _couchbaseScopeProvider;
    private ICouchbaseCollection _collection;
    private IScope _scope;

    public CouchbaseOutboxEventRepository(ICouchbaseScopeProvider couchbaseScopeProvider,
        IOptions<DataStoreSettings> dataStoreSettings)
    {
        _couchbaseScopeProvider = couchbaseScopeProvider;
        _tableName = dataStoreSettings.Value.OutboxEvents ??
                     throw new MissingConfigurationException(nameof(dataStoreSettings.Value.OutboxEvents));
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

    public async Task<List<OutboxEvent>> GetOutboxEventsAsync(IEnumerable<long> ids)
    {
        await InitializeScopeAsync();
        var queryResult = await _scope.QueryAsync<OutboxEvent>(GetOutboxEventsStatement(), options =>
            options
                .Parameter("ids", ids.Select(id => id.ToString()))
                .Readonly(true));

        if (queryResult.MetaData!.Status != QueryStatus.Success)
        {
            throw new CouchbaseQueryFailedException(queryResult.Errors.ToString());
        }

        var outboxEvents = queryResult.Rows;
        return await outboxEvents.ToListAsync();
    }

    public async Task<long> GetNewestEventIdAsync(long latestOffset)
    {
        await InitializeScopeAsync();
        var queryResult = await _scope.QueryAsync<long>(GetNewestEventIdStatement(), options =>
            options
                .Parameter("latestOffset", latestOffset)
                .Readonly(true));

        if (queryResult.MetaData!.Status != QueryStatus.Success)
        {
            throw new CouchbaseQueryFailedException(queryResult.Errors.ToString());
        }

        var newestOffset = await queryResult.Rows.FirstOrDefaultAsync();
        return newestOffset;
    }

    public async Task<List<OutboxEvent>> GetLatestEventsAsync(int batchCount, long newestEventId)
    {
        await InitializeCollectionAsync();
        var queryResult = await _scope.QueryAsync<OutboxEvent>(GetLatestEventsStatement(), options =>
            options
                .Parameter("newestEventId", newestEventId)
                .Parameter("batchCount", batchCount)
                .Readonly(true));


        if (queryResult.MetaData!.Status != QueryStatus.Success)
        {
            throw new CouchbaseQueryFailedException(queryResult.Errors.ToString());
        }

        var outboxEvents = queryResult.Rows;
        return await outboxEvents.ToListAsync();
    }


    private string GetLatestEventsStatement()
    {
        return $"select t.* from `{_tableName}` t where TO_NUMBER(meta().id) >= $newestEventId limit $batchCount";
    }

    private string GetNewestEventIdStatement()
    {
        return $"select raw meta().id from `{_tableName}` where TO_NUMBER(meta().id) > $latestOffset limit 1";
    }

    private string GetOutboxEventsStatement()
    {
        return $"select t.* from `{_tableName}` t where meta().id in $ids";
    }
}