using System.Data;
using System.Threading.Tasks;
using Dapper;
using PollingOutboxPublisher.Database.Providers.Interfaces;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Examples.MsSql;

public class MsSqlExample(IMsSqlConnectionProvider msSqlConnectionProvider)
{
    public async Task PublishMessage()
    {
        //Insert data into MSSQL
        var connection = msSqlConnectionProvider.CreateConnection();
        
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
        return """
               INSERT INTO LogisticsB2BOutbox.OutboxEvents 
                                       ("Key", "Value", Topic, CreatedBy, CreatedDate, Header)
                                   VALUES (@Key, @Value, @Topic, 'Example', GETUTCDATE(), @Header)
               """;
    }
}