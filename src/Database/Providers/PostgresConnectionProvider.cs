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

    public PostgresConnectionProvider(IConfiguration configuration, IOptions<DbCredentialsFileSettings> databaseTbpAuthenticationCredentials)
    {
        if (configuration.GetValue<bool>("UseDbCredentialsFile") is true)
        {
            ValidateTbpAuthenticationCredentials(databaseTbpAuthenticationCredentials.Value);
            var dbCredentials = databaseTbpAuthenticationCredentials.Value;
            var postgresqlUsernamePassword = GetPostgresqlUsernameAndPassword(dbCredentials.FileName);
            _connectionString = GenerateConnectionString(postgresqlUsernamePassword.userName, postgresqlUsernamePassword.password, dbCredentials.Host, dbCredentials.Database, dbCredentials.Port, dbCredentials.ApplicationName,dbCredentials.AdditionalParameters);
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
    
    
    private static (string userName, string password) GetPostgresqlUsernameAndPassword(string fileName)
    {
        if (!File.Exists(fileName))
        {
            throw new FileNotFoundException($"Postgresql credentials file not found: {fileName}");
        }
            
        var postgresqlCredentials= new ConfigurationBuilder()
            .AddJsonFile(fileName)
            .Build();
        
        var username = postgresqlCredentials["username"];
        var password = postgresqlCredentials["password"];
        return (username, password);
    }
    
    private static void ValidateTbpAuthenticationCredentials(DbCredentialsFileSettings credentialses)
    {
        if (string.IsNullOrWhiteSpace(credentialses.FileName))
        {
            throw new MissingConfigurationException("DatabaseTbpAuthenticationCredentials:FileName");
        }
        
        if (string.IsNullOrWhiteSpace(credentialses.Host))
        {
            throw new MissingConfigurationException("DatabaseTbpAuthenticationCredentials:Host");
        }
        
        if (string.IsNullOrWhiteSpace(credentialses.Database))
        {
            throw new MissingConfigurationException("DatabaseTbpAuthenticationCredentials:Database");
        }
        
        if (string.IsNullOrWhiteSpace(credentialses.ApplicationName))
        {
            throw new MissingConfigurationException("DatabaseTbpAuthenticationCredentials:ApplicationName");
        }
        
        if (credentialses.Port == 0)
        {
            throw new MissingConfigurationException("DatabaseTbpAuthenticationCredentials:Port");
        }
    }
    
    private static string GenerateConnectionString(string userName, string password, string host, string database, int port, string appName, string additionalParameters)
    {
        var connectionString = $"User ID={userName};Password={password};Server={host};Port={port};Database={database};ApplicationName={appName};";
        
        if (!string.IsNullOrEmpty(additionalParameters))
            connectionString = string.Concat(connectionString, additionalParameters);;

        return connectionString;
    }
}