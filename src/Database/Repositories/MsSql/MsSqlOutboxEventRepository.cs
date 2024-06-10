using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Options;
using PollingOutboxPublisher.ConfigOptions;
using PollingOutboxPublisher.Database.Providers.Interfaces;
using PollingOutboxPublisher.Database.Repositories.Interfaces;
using PollingOutboxPublisher.Exceptions;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Database.Repositories.MsSql;

[ExcludeFromCodeCoverage]
public class MsSqlOutboxEventRepository : IOutboxEventRepository
{
    private readonly IMsSqlConnectionProvider _msSqlConnectionProvider;
    private readonly string _tableName;

    public MsSqlOutboxEventRepository(IMsSqlConnectionProvider msSqlConnectionProvider,
        IOptions<DataStoreSettings> dataStoreSettings)
    {
        _msSqlConnectionProvider = msSqlConnectionProvider;
        _tableName = dataStoreSettings.Value.OutboxEvents ??
                     throw new MissingConfigurationException(nameof(dataStoreSettings.Value.OutboxEvents));
    }

    public async Task<List<OutboxEvent>> GetOutboxEventsAsync(IEnumerable<long> ids)
    {
        using var connection = _msSqlConnectionProvider.CreateConnectionForReadOnly();
        return (List<OutboxEvent>)await connection.QueryAsync<OutboxEvent>(GetOutboxEventsStatement(),
            new { IdList = ids });
    }

    public async Task<long> GetNewestEventIdAsync(long latestOffset)
    {
        using var connection = _msSqlConnectionProvider.CreateConnectionForReadOnly();
        return await connection.QueryFirstOrDefaultAsync<long>(GetNewestEventIdStatement(),
            new { LatestOffset = latestOffset });
    }

    public async Task<List<OutboxEvent>> GetLatestEventsAsync(int batchCount, long newestEventId)
    {
        using var connection = _msSqlConnectionProvider.CreateConnectionForReadOnly();
        return (List<OutboxEvent>)await connection.QueryAsync<OutboxEvent>(GetLatestEventsStatement(),
            new { BatchCount = batchCount, NewestEventId = newestEventId });
    }


    private string GetLatestEventsStatement()
    {
        return $"""
                SELECT TOP (@BatchCount)
                                            [Id],
                                            [Key],
                                            [Value],
                                            [Topic],
                                            [Header]
                                         FROM {_tableName}
                                         WHERE Id >= @NewestEventId
                """;
    }

    private string GetNewestEventIdStatement()
    {
        return $"""
                SELECT TOP 1 Id 
                                        FROM {_tableName}
                                        WHERE Id > @LatestOffset
                """;
    }

    private string GetOutboxEventsStatement()
    {
        return $"""
                SELECT 
                                            [Id],
                                            [Key],
                                            [Value],
                                            [Topic],
                                            [Header]
                                         FROM {_tableName} 
                                         WHERE Id IN @IdList
                """;
    }
}