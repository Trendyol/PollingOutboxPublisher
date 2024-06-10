using System.Data;
using System.Threading.Tasks;
using Dapper;
using PollingOutboxPublisher.Database.Providers.Interfaces;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Examples;

public class PostgresExample(IPostgresConnectionProvider postgresConnectionProvider)
{
    public async Task PublishMessage()
    {
        //Insert data into MSSQL
        var connection = postgresConnectionProvider.CreateConnection();
        
        for (var i = 0; i < 1000; i++)
        {
            await InsertEvent(connection, i);
        }
    }

    private static async Task InsertEvent(IDbConnection connection, int index)
    {
        await connection.ExecuteAsync(InsertStatement(), new OutboxEvent
        {
            Key = index.ToString(),
            Value = "{ \"key\": \"message\" }",
            Topic = "fulfillment.b2b.test.0",
            Header = "{ \"key\": \"header\" }"
        });
    }

    private static string InsertStatement()
    {
        return $"""
                INSERT INTO shipment_outbox.outbox_events
                   ("key", "value", topic, created_by, created_date, header)
                values (@Key, @Value, @Topic, 'Example', now(), @Header)
                """;
    }
}