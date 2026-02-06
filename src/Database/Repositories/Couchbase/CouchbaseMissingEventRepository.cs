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
public class CouchbaseMissingEventRepository : IMissingEventRepository
{
    private readonly string _tableName;
    private IScope _scope;
    private ICouchbaseCollection _collection;
    private readonly ICouchbaseScopeProvider _couchbaseScopeProvider;

    public CouchbaseMissingEventRepository(ICouchbaseScopeProvider couchbaseScopeProvider,
        IOptions<DataStoreSettings> dataStoreSettings)
    {
        _couchbaseScopeProvider = couchbaseScopeProvider;
        _tableName = dataStoreSettings.Value.MissingEvents ??
                     throw new MissingConfigurationException(nameof(dataStoreSettings.Value.MissingEvents));
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

    public async Task InsertAsync(MissingEvent missingEvent)
    {
        await InitializeCollectionAsync();
        await _collection.UpsertAsync(missingEvent.Id.ToString(), missingEvent);
    }

    public async Task UpdateRetryCountAndExceptionThrownAsync(MissingEvent missingEvent)
    {
        await InitializeCollectionAsync();
        await _collection.UpsertAsync(missingEvent.Id.ToString(), missingEvent);
    }

    public async Task<List<MissingEvent>> GetMissingEventsAsync(int batchCount)
    {
        await InitializeScopeAsync();
        var queryResult = await _scope.QueryAsync<MissingEvent>(GetMissingEventsStatement(), options => options
            .Parameter("batchCount", batchCount)
            .Readonly(true));

        if (queryResult.MetaData!.Status != QueryStatus.Success)
        {
            throw new CouchbaseQueryFailedException(queryResult.Errors.ToString());
        }

        var missingEvents = queryResult.Rows;
        return await missingEvents.ToListAsync();
    }

    public async Task IncrementRetryCountAsync(IEnumerable<long> ids)
    {
        await InitializeScopeAsync();
        var tasks = new List<Task<IQueryResult<dynamic>>>();

        foreach (var id in ids)
        {
            var task = _scope.QueryAsync<dynamic>(IncrementRetryCount(), options => options
                .Parameter("id", id));
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        ThrowExceptionsForFailedQueries(tasks);
    }

    private static void ThrowExceptionsForFailedQueries(List<Task<IQueryResult<dynamic>>> tasks)
    {
        var failedTasks = tasks.Where(task => task.Result.MetaData!.Status != QueryStatus.Success).ToList();
        if (failedTasks.Count > 0)
        {
            var errors = failedTasks.Select(task => task.Result.Errors.ToString()).ToList();
            var joinedErrors = string.Join(" | ", errors);
            throw new CouchbaseQueryFailedException(joinedErrors);
        }
    }

    public async Task DeleteMissingEventsAsync(IReadOnlyCollection<long> ids)
    {
        await InitializeCollectionAsync();
        var tasks = new List<Task>();

        foreach (var id in ids)
        {
            var task = _collection.RemoveAsync(id.ToString());
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
    }

    private string IncrementRetryCount()
    {
        return $"update `{_tableName}` set retryCount = retryCount + 1 where meta().id = $id";
    }

    private string GetMissingEventsStatement()
    {
        return $"select t.* from `{_tableName}` t limit $batchCount";
    }
}