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
public class MsSqlExceededEventRepository : IExceededEventRepository
{
    private readonly IMsSqlConnectionProvider _msSqlConnectionProvider;
    private readonly string _tableName;

    public MsSqlExceededEventRepository(IMsSqlConnectionProvider msSqlConnectionProvider,
        IOptions<DataStoreSettings> dataStoreSettings)
    {
        _msSqlConnectionProvider = msSqlConnectionProvider;
        _tableName = dataStoreSettings.Value.ExceededEvents ??
                     throw new MissingConfigurationException(nameof(dataStoreSettings.Value
                         .ExceededEvents));
    }

    public async Task InsertAsync(ExceededEvent exceededEvent)
    {
        using var connection = _msSqlConnectionProvider.CreateConnection();
        await connection.ExecuteAsync(InsertStatement(), exceededEvent);
    }

    private string InsertStatement()
    {
        return $"""
                INSERT INTO {_tableName} 
                                           (Id, MissedDate, RetryCount, ExceededDate, ExceptionThrown) 
                                        VALUES (@Id, @MissedDate, @RetryCount, @ExceededDate, @ExceptionThrown)
                """;
    }
}