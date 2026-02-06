using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PollingOutboxPublisher.ConfigOptions;
using PollingOutboxPublisher.Database.Providers.Interfaces;
using PollingOutboxPublisher.Database.Repositories.Interfaces;
using PollingOutboxPublisher.Exceptions;
using PollingOutboxPublisher.Extensions;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Database.Repositories.MsSql;

[ExcludeFromCodeCoverage]
public class MsSqlMissingEventRepository : IMissingEventRepository
{
    private readonly string _tableName;
    private readonly IMsSqlConnectionProvider _msSqlConnectionProvider;
    private readonly ILogger<MsSqlMissingEventRepository> _logger;

    public MsSqlMissingEventRepository(IMsSqlConnectionProvider msSqlConnectionProvider,
        ILogger<MsSqlMissingEventRepository> logger, IOptions<DataStoreSettings> dataStoreSettings)
    {
        _msSqlConnectionProvider = msSqlConnectionProvider;
        _logger = logger;
        _tableName = dataStoreSettings.Value.MissingEvents ??
                     throw new MissingConfigurationException(nameof(dataStoreSettings.Value.MissingEvents));
    }

    public async Task InsertAsync(MissingEvent missingEvent)
    {
        using var connection = _msSqlConnectionProvider.CreateConnection();
        await connection.ExecuteAsync(InsertStatement(), missingEvent);
    }

    public async Task UpdateRetryCountAndExceptionThrownAsync(MissingEvent missingEvent)
    {
        using var connection = _msSqlConnectionProvider.CreateConnection();
        await connection.ExecuteAsync(UpdateRetryCountAndExceptionThrownAsyncStatement(), missingEvent);
    }

    public async Task<List<MissingEvent>> GetMissingEventsAsync(int batchCount)
    {
        using var connection = _msSqlConnectionProvider.CreateConnectionForReadOnly();

        return (List<MissingEvent>)await connection.QueryAsync<MissingEvent>(
            GetMissingEventsStatement(), new { BatchCount = batchCount });
    }

    public async Task IncrementRetryCountAsync(IEnumerable<long> ids)
    {
        using var connection = _msSqlConnectionProvider.CreateConnection();
        await connection.ExecuteAsync(IncrementRetryCountOfListStatement(), new { IdList = ids });
    }

    public async Task DeleteMissingEventsAsync(IReadOnlyCollection<long> idList)
    {
        using var connection = _msSqlConnectionProvider.CreateConnection();
        await connection.ExecuteAsyncWithRetry(DeleteMissingEventsStatement(), _logger, new { IdList = idList });
    }

    private string DeleteMissingEventsStatement()
    {
        return $"""
                DELETE {_tableName}
                                        WHERE Id IN @IdList
                """;
    }

    private string IncrementRetryCountOfListStatement()
    {
        return $"""
                UPDATE {_tableName} 
                                            SET RetryCount=RetryCount+1
                                        WHERE Id IN @IdList
                """;
    }

    private string GetMissingEventsStatement()
    {
        return $"""
                SELECT TOP (@BatchCount)
                                              Id,
                                              MissedDate,
                                              RetryCount,
                                              ExceptionThrown
                                            FROM {_tableName}
                """;
    }

    private string UpdateRetryCountAndExceptionThrownAsyncStatement()
    {
        return $"""
                UPDATE {_tableName} 
                                        SET RetryCount= @RetryCount, ExceptionThrown = @ExceptionThrown
                                    WHERE Id = @Id
                """;
    }

    private string InsertStatement()
    {
        return $"""
                INSERT INTO {_tableName} 
                                        (Id, MissedDate, RetryCount, ExceptionThrown)
                                    VALUES (@Id, @MissedDate, @RetryCount, @ExceptionThrown)
                """;
    }
}