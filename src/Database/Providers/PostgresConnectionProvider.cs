using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;
using PollingOutboxPublisher.Database.Providers.Interfaces;
using PollingOutboxPublisher.Exceptions;

namespace PollingOutboxPublisher.Database.Providers;

public class PostgresConnectionProvider : IPostgresConnectionProvider
{
    private readonly string _connectionString;

    public PostgresConnectionProvider(IConfiguration configuration)
    {
        var connectionString = configuration.GetValue<string>("ConnectionString");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new MissingConfigurationException("ConnectionString");
        }

        _connectionString = connectionString;
    }

    public IDbConnection CreateConnection()
        => new NpgsqlConnection(_connectionString);
        
    public IDbConnection CreateConnectionForReadOnly()
        => new NpgsqlConnection(_connectionString);
    
}