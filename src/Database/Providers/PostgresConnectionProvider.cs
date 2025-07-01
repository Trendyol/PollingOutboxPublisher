using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;
using PollingOutboxPublisher.ConfigOptions;
using PollingOutboxPublisher.Database.Providers.Interfaces;
using PollingOutboxPublisher.Exceptions;

namespace PollingOutboxPublisher.Database.Providers;

[ExcludeFromCodeCoverage]
public class PostgresConnectionProvider : IPostgresConnectionProvider
{
    private readonly string _connectionString;

    public PostgresConnectionProvider(IConfiguration configuration, IOptions<DatabaseTbpAuthenticationCredentials> databaseTbpAuthenticationCredentials)
    {
        if (configuration.GetValue<bool>("IsTbpAuthenticationEnabled") is true)
        {
            ValidateTbpAuthenticationCredentials(databaseTbpAuthenticationCredentials.Value);
            var dbCredentials = databaseTbpAuthenticationCredentials.Value;
            var postgresqlUsernamePassword = GetPostgresqlUsernameAndPassword(dbCredentials.ClusterName);
            _connectionString = GenerateConnectionString(postgresqlUsernamePassword.userName, postgresqlUsernamePassword.password, dbCredentials.Host, dbCredentials.Database, dbCredentials.Port, dbCredentials.ApplicationName);
        }
        else
        {
            var connectionString = configuration.GetValue<string>("ConnectionString");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new MissingConfigurationException("ConnectionString");
            }

            _connectionString = connectionString;
        }
    }

    public IDbConnection CreateConnection()
        => new NpgsqlConnection(_connectionString);
        
    public IDbConnection CreateConnectionForReadOnly()
        => new NpgsqlConnection(_connectionString);
    
    
    private static (string userName, string password) GetPostgresqlUsernameAndPassword(string clusterName)
    {
        var filePath = Path.Combine("config", $"postgresql-{clusterName}.json");

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Postgresql credentials file not found: {filePath}");
        }
            
        var postgresqlCredentials= new ConfigurationBuilder()
            .AddJsonFile(filePath)
            .Build();
        
        var username = postgresqlCredentials["username"];
        var password = postgresqlCredentials["password"];
        return (username, password);
    }
    
    private static void ValidateTbpAuthenticationCredentials(DatabaseTbpAuthenticationCredentials credentials)
    {
        if (string.IsNullOrWhiteSpace(credentials.ClusterName))
        {
            throw new MissingConfigurationException("DatabaseTbpAuthenticationCredentials:ClusterName");
        }
        
        if (string.IsNullOrWhiteSpace(credentials.Host))
        {
            throw new MissingConfigurationException("DatabaseTbpAuthenticationCredentials:Host");
        }
        
        if (string.IsNullOrWhiteSpace(credentials.Database))
        {
            throw new MissingConfigurationException("DatabaseTbpAuthenticationCredentials:Database");
        }
        
        if (string.IsNullOrWhiteSpace(credentials.ApplicationName))
        {
            throw new MissingConfigurationException("DatabaseTbpAuthenticationCredentials:ApplicationName");
        }
        
        if (credentials.Port == 0)
        {
            throw new MissingConfigurationException("DatabaseTbpAuthenticationCredentials:Port");
        }
    }
    
    private static string GenerateConnectionString(string userName, string password, string host, string database, int port, string appName)
    {
        return $"User ID={userName};Password={password};Server={host};Port={port};Database={database};Pooling=true;TrustServerCertificate=true;ApplicationName={appName};";
    }

    
}