using System.Data;

namespace PollingOutboxPublisher.Database.Providers.Interfaces;

public interface IPostgresConnectionProvider
{
    IDbConnection CreateConnection();
    IDbConnection CreateConnectionForReadOnly();
}