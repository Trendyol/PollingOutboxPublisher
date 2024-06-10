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

namespace PollingOutboxPublisher.Database.Repositories.Postgres
{
    [ExcludeFromCodeCoverage]
    public class PostgresOutboxEventRepository : IOutboxEventRepository
    {
        private readonly IPostgresConnectionProvider _postgresConnectionProvider;
        private readonly string _tableName;

        public PostgresOutboxEventRepository(IPostgresConnectionProvider postgresConnectionProvider,
            IOptions<DataStoreSettings> tableNameSettings)
        {
            _postgresConnectionProvider = postgresConnectionProvider;
            _tableName = tableNameSettings.Value.OutboxEvents ??
                         throw new MissingConfigurationException(nameof(tableNameSettings.Value.OutboxEvents));
        }

        public async Task<List<OutboxEvent>> GetOutboxEventsAsync(IEnumerable<long> idList)
        {
            using var connection = _postgresConnectionProvider.CreateConnection();
            return (List<OutboxEvent>)await connection.QueryAsync<OutboxEvent>(GetOutboxEventsStatement(),
                new { IdList = idList });
        }

        public async Task<long> GetNewestEventIdAsync(long latestOffset)
        {
            using var connection = _postgresConnectionProvider.CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<long>(GetNewestEventIdStatement(),
                new { LatestOffset = latestOffset });
        }

        public async Task<List<OutboxEvent>> GetLatestEventsAsync(int batchCount, long newestEventId)
        {
            using var connection = _postgresConnectionProvider.CreateConnection();
            return (List<OutboxEvent>)await connection.QueryAsync<OutboxEvent>(GetLatestEventsStatement(),
                new { BatchCount = batchCount, NewestEventId = newestEventId });
        }


        private string GetLatestEventsStatement()
        {
            return $"""
                    SELECT 
                                                id,
                                                key,
                                                value,
                                                topic,
                                                header
                                             FROM {_tableName}
                                             WHERE id >= @NewestEventId
                                             LIMIT @BatchCount
                    """;
        }

        private string GetNewestEventIdStatement()
        {
            return $"""
                    SELECT id 
                                            FROM {_tableName}
                                            WHERE id > @LatestOffset
                                            LIMIT 1
                    """;
        }

        private string GetOutboxEventsStatement()
        {
            return $"""
                    SELECT 
                                                id,
                                                key,
                                                value,
                                                topic,
                                                header
                                             FROM {_tableName}
                                             WHERE id = ANY(@IdList)
                    """;
        }
    }
}