using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PollingOutboxPublisher.ConfigOptions;
using PollingOutboxPublisher.Coordinators.MissingCoordinator.Services;
using PollingOutboxPublisher.Coordinators.MissingCoordinator.Services.Interfaces;
using PollingOutboxPublisher.Coordinators.OutboxCoordinator.Services;
using PollingOutboxPublisher.Coordinators.OutboxCoordinator.Services.Interfaces;
using PollingOutboxPublisher.Coordinators.Services;
using PollingOutboxPublisher.Coordinators.Services.Interfaces;
using PollingOutboxPublisher.Daemon;
using PollingOutboxPublisher.Database.Providers;
using PollingOutboxPublisher.Database.Providers.Interfaces;
using PollingOutboxPublisher.Database.Repositories.Couchbase;
using PollingOutboxPublisher.Database.Repositories.Interfaces;
using PollingOutboxPublisher.Database.Repositories.MsSql;
using PollingOutboxPublisher.Database.Repositories.Postgres;
using PollingOutboxPublisher.Exceptions;
using PollingOutboxPublisher.Helper;
using PollingOutboxPublisher.Redis;
using PollingOutboxPublisher.Redis.Interfaces;
using Serilog;

namespace PollingOutboxPublisher;

[ExcludeFromCodeCoverage]
public static class Program
{
    public static async Task Main(string[] args)
    {
        CheckConfigFileExistence();
        var host = CreateHostBuilder(args).Build();

        //ExampleRunner.RunMsSqlExample(host);
        
        await host.RunAsync();
    }

    private static void CheckConfigFileExistence()
    {
        var isConfigExist = File.Exists(Directory.GetCurrentDirectory() + "/config/config.json");
        if (!isConfigExist)
            throw new FileNotFoundException("File not found", "config.json");

        var isSecretExist = File.Exists(Directory.GetCurrentDirectory() + "/config/secret.json");
        if (!isSecretExist)
            throw new FileNotFoundException("File not found", "secret.json");
    }

    private static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(
                (_, builder) =>
                {
                    builder.AddJsonFile("config/config.json", true, true)
                        .AddJsonFile("config/secret.json", true, true)
                        .Build();
                })
            .ConfigureServices((hostContext, services) =>
            {
                RegisterHostedServices(services);
                RegisterKafkaServices(services);
                RegisterCoordinatorServices(services);
                RegisterSerilog(services);
                RegisterConfigs(services, hostContext);
                RegisterSelectedDatabaseRepositories(hostContext, services);
                RegisterMasterPodServices(hostContext, services);
            });
    }

    private static void RegisterCoordinatorServices(IServiceCollection services)
    {
        services.AddTransient<IPollingSource, PollingSource>();
        services.AddTransient<IPollingMissingEventsSource, PollingMissingEventsSource>();
        services.AddTransient<IOutboxDispatcher, OutboxDispatcher>();
        services.AddTransient<IOffsetSetter, OffsetSetter>();
        services.AddTransient<IMissingDetector, MissingDetector>();
        services.AddTransient<IMissingEventCleaner, MissingEventCleaner>();
    }

    private static void RegisterKafkaServices(IServiceCollection services)
    {
        services.AddSingleton<IKafkaService, KafkaService>();
        services.AddSingleton<IKafkaProducer, KafkaProducer>();
    }

    private static void RegisterHostedServices(IServiceCollection services)
    {
        services.AddHostedService<HealthCheckDaemon>();
        services.AddHostedService<OutboxEventsDaemon>();
        services.AddHostedService<MissingEventsDaemon>();
    }

    private static void RegisterConfigs(IServiceCollection services, HostBuilderContext hostContext)
    {
        services.Configure<BenchmarkOptions>(
            hostContext.Configuration.GetSection(nameof(BenchmarkOptions)));
        services.Configure<WorkerSettings>(hostContext.Configuration.GetSection(nameof(WorkerSettings)));
        services.Configure<DataStoreSettings>(
            hostContext.Configuration.GetSection(nameof(DataStoreSettings)));
        services.Configure<MasterPodSettings>(
            hostContext.Configuration.GetSection(nameof(MasterPodSettings)));
        services.Configure<Kafka>(
            hostContext.Configuration.GetSection(nameof(Kafka)));
        services.Configure<ConfigOptions.Couchbase>(
            hostContext.Configuration.GetSection(nameof(ConfigOptions.Couchbase)));
        services.Configure<ConfigOptions.Redis>(
            hostContext.Configuration.GetSection(nameof(ConfigOptions.Redis)));
    }

    private static void RegisterSerilog(IServiceCollection services)
    {
        services.AddSerilog((provider, loggerConfiguration) =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            loggerConfiguration.ReadFrom.Configuration(configuration);
        });
    }

    private static void RegisterSelectedDatabaseRepositories(HostBuilderContext hostContext, IServiceCollection services)
    {
        var databaseType = hostContext.Configuration.GetValue<string>("DataStoreSettings:DatabaseType");

        switch (databaseType)
        {
            case Constants.DatabaseTypes.MSSQL:
                RegisterSqlRepositories(services);
                break;
            case Constants.DatabaseTypes.Couchbase:
                RegisterCouchbaseRepositories(services);
                break;
            case Constants.DatabaseTypes.PostgreSQL:
                RegisterPostgreSQLRepositories(services);
                break;
            default:
                throw new UnsupportedDatabaseTypeException(databaseType);
        }
    }

    private static void RegisterCouchbaseRepositories(IServiceCollection services)
    {
        services.AddSingleton<ICouchbaseScopeProvider, CouchbaseScopeProvider>();
        services.AddSingleton<IExceededEventRepository, CouchbaseExceededEventRepository>();
        services.AddSingleton<IMissingEventRepository, CouchbaseMissingEventRepository>();
        services.AddSingleton<IOutboxEventRepository, CouchbaseOutboxEventRepository>();
        services.AddSingleton<IOutboxOffsetRepository, CouchbaseOutboxOffsetRepository>();
    }

    private static void RegisterSqlRepositories(IServiceCollection services)
    {
        services.AddSingleton<IMsSqlConnectionProvider, MsSqlConnectionProvider>();
        services.AddTransient<IExceededEventRepository, MsSqlExceededEventRepository>();
        services.AddTransient<IMissingEventRepository, MsSqlMissingEventRepository>();
        services.AddTransient<IOutboxEventRepository, MsSqlOutboxEventRepository>();
        services.AddTransient<IOutboxOffsetRepository, MsSqlOutboxOffsetRepository>();
    }

    private static void RegisterPostgreSQLRepositories(IServiceCollection services)
    {
        services.AddSingleton<IPostgresConnectionProvider, PostgresConnectionProvider>();
        services.AddTransient<IExceededEventRepository, PostgresExceededEventRepository>();
        services.AddTransient<IMissingEventRepository, PostgresMissingEventRepository>();
        services.AddTransient<IOutboxEventRepository, PostgresOutboxEventRepository>();
        services.AddTransient<IOutboxOffsetRepository, PostgresOutboxOffsetRepository>();
    }

    private static void RegisterMasterPodServices(HostBuilderContext hostContext, IServiceCollection services)
    {
        var isMasterPodActive = hostContext.Configuration.GetValue<bool>("MasterPodSettings:IsActive");
        if (isMasterPodActive)
        {
            services.AddHostedService<MasterPodDaemon>();
            services.AddSingleton<IMasterPodChecker, MasterPodChecker>();
            services.AddSingleton<IRedisLockManager, RedisLockManager>();
            services.AddSingleton<IRedisClientFactory, RedisClientFactory>();
        }
        else
        {
            services.AddSingleton<IMasterPodChecker, NotActiveMasterPodChecker>();
        }
    }
}