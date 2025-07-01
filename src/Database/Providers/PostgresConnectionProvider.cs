using System;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Extensions.Configuration;
using Npgsql;
using PollingOutboxPublisher.Database.Providers.Interfaces;
using PollingOutboxPublisher.Exceptions;

namespace PollingOutboxPublisher.Database.Providers;

[ExcludeFromCodeCoverage]
public class PostgresConnectionProvider : IPostgresConnectionProvider
{
    private readonly string _connectionString;

    public PostgresConnectionProvider(IConfiguration configuration)
    {
        if (configuration.GetValue<bool>("IsTbpAuthenticationEnabled") is true)
        {
            var postgresqlCredentials = GetTbpAuthenticationCredentials(configuration);
            var postgresqlUsernamePassword = GetPostgresqlUsernameAndPassword(postgresqlCredentials.clusterName);
            _connectionString = GenerateConnectionString(postgresqlUsernamePassword.userName, postgresqlUsernamePassword.password, postgresqlCredentials.host, postgresqlCredentials.database, postgresqlCredentials.port);
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

    private static (string clusterName, string host, string database, int port) GetTbpAuthenticationCredentials(IConfiguration configuration)
    {
        var clusterName = configuration.GetValue<string>("TbpAuthenticationCredentials:ClusterName");
        if (string.IsNullOrWhiteSpace(clusterName))
        {
            throw new MissingConfigurationException("TbpAuthenticationCredentials:ClusterName");
        }
        
        var host = configuration.GetValue<string>("TbpAuthenticationCredentials:Host");
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new MissingConfigurationException("TbpAuthenticationCredentials:Host");
        }
        
        var dbName = configuration.GetValue<string>("TbpAuthenticationCredentials:Database");
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new MissingConfigurationException("TbpAuthenticationCredentials:Database");
        }
        
        var port = configuration.GetValue<string>("TbpAuthenticationCredentials:Port");
        if ((string.IsNullOrWhiteSpace(port) || int.TryParse(port, out int parsedPort)) == false)
        {
            throw new MissingConfigurationException("TbpAuthenticationCredentials:Port");
        }
        
        return (clusterName, host, dbName, int.Parse(port!));
    }
    
    private static string GenerateConnectionString(string userName, string password, string host, string database, int port)
    {
        return $"User ID={userName};Password={password};Server={host};Port={port};Database={database};Pooling=true;TrustServerCertificate=true;";
    }

    
}