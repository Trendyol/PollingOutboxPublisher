using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PollingOutboxPublisher.Database.Providers.Interfaces;

namespace PollingOutboxPublisher.Examples;

public static class ExampleRunner
{
    public static void RunCouchbaseExample(IHost host)
    {
        var couchbaseScopeProvider = host.Services.GetRequiredService<ICouchbaseScopeProvider>();
        var couchbaseExample = new CouchbaseExample(couchbaseScopeProvider);
        couchbaseExample.PublishMessage().Wait();
    }
    
    public static void RunMsSqlExample(IHost host)
    {
        var msSqlConnectionProvider = host.Services.GetRequiredService<IMsSqlConnectionProvider>();
        var mssqlExample = new MsSqlExample(msSqlConnectionProvider);
        mssqlExample.PublishMessage().Wait();
    }
    
    public static void RunPostgresExample(IHost host)
    {
        var postgresConnectionProvider = host.Services.GetRequiredService<IPostgresConnectionProvider>();
        var mssqlExample = new PostgresExample(postgresConnectionProvider);
        mssqlExample.PublishMessage().Wait();
    }
}