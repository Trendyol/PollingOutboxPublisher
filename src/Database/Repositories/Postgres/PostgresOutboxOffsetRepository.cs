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

namespace PollingOutboxPublisher.Database.Repositories.Postgres
{
    [ExcludeFromCodeCoverage]
    public class PostgresOutboxOffsetRepository : IOutboxOffsetRepository
    {
        private readonly string _tableName;
        private readonly ILogger<PostgresOutboxOffsetRepository> _logger;
        private readonly IPostgresConnectionProvider _postgresConnectionProvider;

        public PostgresOutboxOffsetRepository(ILogger<PostgresOutboxOffsetRepository> logger, IPostgresConnectionProvider postgresConnectionProvider, IOptions<DataStoreSettings> tableNameSettings)
        {
            _logger = logger;
            _postgresConnectionProvider = postgresConnectionProvider;
            _tableName = tableNameSettings.Value.OutboxOffset ??
                         throw new MissingConfigurationException(nameof(tableNameSettings.Value.OutboxOffset));
        }

        public async Task<long?> GetLatestOffsetAsync()
        {
            using var connection = _postgresConnectionProvider.CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<long?>(GetLatestOffsetStatement());
        }

        public async Task UpdateOffsetAsync(long latestOffSet)
        {
            using var connection = _postgresConnectionProvider.CreateConnection();
            await connection.ExecuteAsyncWithRetry(UpdateOffsetStatement(), _logger, new { LatestOffSet = latestOffSet });
        }

        private string UpdateOffsetStatement()
        {
            return $"""
                    UPDATE {_tableName}
                                                SET "offset"= @LatestOffSet
                    """;
        }

        private string GetLatestOffsetStatement()
        {
            return $"""
                    SELECT "offset"
                                            FROM {_tableName}
                                            LIMIT 1
                    """;
        }
    }
}