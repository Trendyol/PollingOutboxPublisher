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
    public class PostgresExceededEventRepository : IExceededEventRepository
    {
        private readonly IPostgresConnectionProvider _postgresConnectionProvider;
        private readonly string _tableName;

        public PostgresExceededEventRepository(IPostgresConnectionProvider postgresConnectionProvider,  IOptions<DataStoreSettings> tableNameSettings)
        {
            _postgresConnectionProvider = postgresConnectionProvider;
            _tableName = tableNameSettings.Value.ExceededEvents ??
                         throw new MissingConfigurationException(nameof(tableNameSettings.Value
                             .ExceededEvents));
        }

        public async Task InsertAsync(ExceededEvent exceededEvent)
        {
            using var connection = _postgresConnectionProvider.CreateConnection();
            await connection.ExecuteAsync(InsertStatement(), exceededEvent);
        }

        private string InsertStatement()
        {
            return $"""
                    INSERT INTO {_tableName} 
                                               (id, missed_date, retry_count, exceeded_date, exception_thrown) 
                                            VALUES (@Id, @MissedDate, @RetryCount, @ExceededDate, @ExceptionThrown)
                    """;
        }
    }
}