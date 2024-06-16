using System.Threading.Tasks;
using Couchbase.KeyValue;
using PollingOutboxPublisher.Database.Providers.Interfaces;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Examples.Couchbase;

public class CouchbaseExample(ICouchbaseScopeProvider couchbaseScopeProvide)
{
    public async Task PublishMessage()
    {
        var scope = couchbaseScopeProvide.GetScopeAsync().Result;
        var offsetCollection = await scope.CollectionAsync("outbox-offset");
        var eventCollection = await scope.CollectionAsync("outbox-events");

        for (var i = 0; i < 1000; i++)
        {
            await InsertEvent(eventCollection, offsetCollection);
        }
    }

    private static async Task InsertEvent(ICouchbaseCollection eventCollection, ICouchbaseCollection collection)
    {
        //get the next id from the event-id-counter
        var id = (await collection.Binary.IncrementAsync("event-id-counter")).Content;
            
        //insert new event using into the outbox-events collection 
        await eventCollection.InsertAsync(id.ToString(), new OutboxEvent
        {
            Id = (long)id,
            Key = id.ToString(),
            Value = "{ \"key\": \"message\" }",
            Topic = "fulfillment.b2b.test.0",
            Header = "{ \"key\": \"header\" }"
        });
    }
}