using System.Data;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PollingOutboxPublisher.Database.Providers.Interfaces;
using PollingOutboxPublisher.Exceptions;

namespace PollingOutboxPublisher.Database.Providers;

public class MsSqlConnectionProvider : IMsSqlConnectionProvider
{
    private readonly string _connectionString;
    private readonly string _readOnlyConnectionString;

    public MsSqlConnectionProvider(IConfiguration configuration)
    {
        var connectionString = configuration.GetValue<string>("ConnectionString");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new MissingConfigurationException("ConnectionString");
        }

        _connectionString = connectionString;
        _readOnlyConnectionString = configuration.GetValue<string>("ReadOnlyConnectionString") ?? _connectionString;
    }

    public IDbConnection CreateConnection()
        => new SqlConnection(_connectionString);

    public IDbConnection CreateConnectionForReadOnly()
        => new SqlConnection(_readOnlyConnectionString);
}