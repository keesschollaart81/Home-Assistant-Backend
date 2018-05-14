
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
      public class Owntracks
    {
        public string _type { get; set; }
        public string tid { get; set; }
        public string acc { get; set; }
        public string batt { get; set; }
        public string conn { get; set; }
        public string lat { get; set; }
        public string lon { get; set; }
        public string tst { get; set; }
        public string _cp { get; set; }
        public string alt { get; set; }
        public string vac { get; set; }
        public string t { get; set; }

    }
        
    public class Trail : Owntracks
    {
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public string Topic { get; set; }
        public string QosLevel { get; set; }
        public bool Retain { get; set; }
    }
}
