
using System;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using CaseOnline.Azure.WebJobs.Extensions.Mqtt.Messaging;
using CaseOnline.Azure.WebJobs.Extensions.Mqtt;
using Microsoft.Extensions.Logging;

namespace Functions
{
    public static class OwntracksFunctions
    {
        [FunctionName("GPSTrail")]
        public static void SimpleFunction(
            [MqttTrigger(new[] { "owntracks/kees/kees01", "owntracks/marleen/marleen01" })]IMqttMessage message,
            ILogger log,
            [Table("Locations", Connection = "AzureWebJobsStorage")] out Trail trail)
        {
            var body = Encoding.UTF8.GetString(message.GetMessage());

            log.LogInformation($"Message from topic {message.Topic} body: {body}");

            trail = JsonConvert.DeserializeObject<Trail>(body);
            trail.PartitionKey = message.Topic.Replace("/", "_");
            trail.RowKey = DateTime.Now.Ticks.ToString();
            trail.QosLevel = message.QosLevel.ToString();
            trail.Retain = message.Retain;
        }
    }
}
