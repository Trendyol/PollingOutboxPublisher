using System.Data;

namespace PollingOutboxPublisher.Database.Providers.Interfaces;

public interface IMsSqlConnectionProvider
{
    IDbConnection CreateConnection();
    IDbConnection CreateConnectionForReadOnly();
}