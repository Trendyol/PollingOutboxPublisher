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

namespace PollingOutboxPublisher.Database.Repositories.MsSql;

[ExcludeFromCodeCoverage]
public class MsSqlOutboxOffsetRepository : IOutboxOffsetRepository
{
    private readonly string _tableName;
    private readonly IMsSqlConnectionProvider _msSqlConnectionProvider;
    private readonly ILogger<MsSqlOutboxOffsetRepository> _logger;

    public MsSqlOutboxOffsetRepository(IMsSqlConnectionProvider msSqlConnectionProvider,
        ILogger<MsSqlOutboxOffsetRepository> logger, IOptions<DataStoreSettings> dataStoreSettings)
    {
        _msSqlConnectionProvider = msSqlConnectionProvider;
        _logger = logger;
        _tableName = dataStoreSettings.Value.OutboxOffset ??
                     throw new MissingConfigurationException(nameof(dataStoreSettings.Value.OutboxOffset));
    }

    public async Task<long?> GetLatestOffsetAsync()
    {
        using var connection = _msSqlConnectionProvider.CreateConnectionForReadOnly();
        return await connection.QueryFirstOrDefaultAsync<long?>(GetLatestOffsetStatement());
    }

    public async Task UpdateOffsetAsync(long latestOffSet)
    {
        using var connection = _msSqlConnectionProvider.CreateConnection();
        await connection.ExecuteAsyncWithRetry(UpdateOffsetStatement(), _logger,
            new { LatestOffSet = latestOffSet });
    }

    private string UpdateOffsetStatement()
    {
        return $"""
                UPDATE {_tableName} 
                                            SET Offset= @LatestOffSet
                """;
    }

    private string GetLatestOffsetStatement()
    {
        return $"""
                SELECT TOP 1 Offset 
                                        FROM {_tableName}
                """;
    }
}