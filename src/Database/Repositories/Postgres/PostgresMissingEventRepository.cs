using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewRelic.Api.Agent;
using PollingOutboxPublisher.ConfigOptions;
using PollingOutboxPublisher.Database.Providers.Interfaces;
using PollingOutboxPublisher.Database.Repositories.Interfaces;
using PollingOutboxPublisher.Exceptions;
using PollingOutboxPublisher.Extensions;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Database.Repositories.Postgres
{
    [ExcludeFromCodeCoverage]
    public class PostgresMissingEventRepository : IMissingEventRepository
    {
        private readonly string _tableName;
        private readonly ILogger<PostgresMissingEventRepository> _logger;
        private readonly IPostgresConnectionProvider _postgresConnectionProvider;

        public PostgresMissingEventRepository(ILogger<PostgresMissingEventRepository> logger, IPostgresConnectionProvider postgresConnectionProvider, IOptions<DataStoreSettings> tableNameSettings)
        {
            _logger = logger;
            _postgresConnectionProvider = postgresConnectionProvider;
            _tableName = tableNameSettings.Value.MissingEvents ??
                         throw new MissingConfigurationException(nameof(tableNameSettings.Value.MissingEvents));
        }

        [Trace]
        public async Task InsertAsync(MissingEvent missingEvent)
        {
            using var connection = _postgresConnectionProvider.CreateConnection();
            await connection.ExecuteAsync(InsertStatement(), missingEvent);
        }

        public async Task UpdateRetryCountAndExceptionThrownAsync(MissingEvent missingEvent)
        {
            using var connection = _postgresConnectionProvider.CreateConnection();
            await connection.ExecuteAsync(UpdateRetryCountAndExceptionThrownAsyncStatement(), missingEvent);
        }

        public async Task<List<MissingEvent>> GetMissingEventsAsync(int batchCount)
        {
            using var connection = _postgresConnectionProvider.CreateConnection();
            return (List<MissingEvent>)await connection.QueryAsync<MissingEvent>(
                GetMissingEventsStatement(), new { BatchCount = batchCount });
        }

        public async Task IncrementRetryCountAsync(IEnumerable<long> idList)
        {
            using var connection = _postgresConnectionProvider.CreateConnection();
            await connection.ExecuteAsync(IncrementRetryCountOfListStatement(), new { IdList = idList });
        }

        public async Task DeleteMissingEventsAsync(IReadOnlyCollection<long> idList)
        {
            using var connection = _postgresConnectionProvider.CreateConnection();
            await connection.ExecuteAsyncWithRetry(DeleteMissingEventsStatement(), _logger, new { IdList = idList });
        }

        private string DeleteMissingEventsStatement()
        {
            return $"""
                    DELETE FROM {_tableName}
                                            WHERE id = ANY(@IdList)
                    """;
        }

        private string IncrementRetryCountOfListStatement()
        {
            return $"""
                    UPDATE {_tableName} 
                                                SET retry_count=retry_count+1
                                            WHERE id = ANY(@IdList)
                    """;
        }

        private string GetMissingEventsStatement()
        {
            return $"""
                    SELECT  id,
                                                  missed_date AS MissedDate,
                                                  retry_count AS RetryCount,
                                                  exception_thrown  AS ExceptionThrown FROM {_tableName} LIMIT (@BatchCount)
                    """;
        }

        private string UpdateRetryCountAndExceptionThrownAsyncStatement()
        {
            return $"""
                    UPDATE {_tableName} 
                                            SET retry_count= @RetryCount, exception_thrown = @ExceptionThrown
                                        WHERE id = @Id
                    """;
        }

        private string InsertStatement()
        {
            return $"""
                    INSERT INTO {_tableName} 
                                            (id, missed_date, retry_count, exception_thrown)
                                        VALUES (@Id, @MissedDate, @RetryCount, @ExceptionThrown)
                    """;
        }
    }
}