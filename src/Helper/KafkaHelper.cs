using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Confluent.Kafka;
using Newtonsoft.Json;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Helper;

[ExcludeFromCodeCoverage]
public static class KafkaHelper
{
    public static KafkaMessageModel PrepareKafkaMessageModel(this OutboxEvent message, string topicName, string key)
    {
            var kafkaMessage = new Message<string, string>
            {
                Key = key,
                Timestamp = new Timestamp(DateTime.Now),
                Value = message.Value
            };

            SetHeader(kafkaMessage, message.Header);
 
            var response = new KafkaMessageModel()
            {
                Message = kafkaMessage,
                TopicName = topicName
            };

            return response;
        }
        
    private static void SetHeader(Message<string, string> message, string messageHeader)
    {
            if (string.IsNullOrEmpty(messageHeader))
            {
                return;
            }
            var headerDictionary = JsonConvert.DeserializeObject<Dictionary<string,string>>(messageHeader);
            
            message.Headers ??= new Headers();
            
            foreach (var header in headerDictionary)
            {
                AddHeader(message, header.Key, header.Value);
            }
        }

    private static void AddHeader(this Message<string, string> message, string key, string value)
    {
            var bytes = value == null
                ? Array.Empty<byte>()
                : Encoding.UTF8.GetBytes(value);
            var header = new Header(key, bytes);
            message.Headers.Add(header);
        }
}