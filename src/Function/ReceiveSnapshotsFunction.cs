
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using CaseOnline.Azure.WebJobs.Extensions.Mqtt.Messaging;
using CaseOnline.Azure.WebJobs.Extensions.Mqtt;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System;
using System.Configuration;
using Microsoft.Extensions.Configuration;

namespace Functions
{
    public static class Functions
    {
        [FunctionName("MotionFunction")]
        public static async Task MotionFunction(
        [MqttTrigger("dafang/dafang/motion", ConnectionString = "MqttConnectionForMotion")]IMqttMessage snapshop,
        [Blob("snapshots/{sys.utcnow}.png", FileAccess.Write)] Stream outputBlob,
        ILogger log,
        ExecutionContext context)
        {
            log.LogInformation("Receiving blob");

            var config = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var camUrl = config["CamUrl"];
            var username = config["CamUsername"];
            var password = config["CamPassword"];

            var bytes = snapshop.GetMessage();
            var on = Encoding.UTF8.GetString(bytes) == "ON";

            if (on)
            {
                var client = new HttpClient();
                var authHeaderValue = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes($"{username}:{password}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeaderValue);

                var response = await client.GetAsync(camUrl);
                var outBytes = await response.Content.ReadAsByteArrayAsync();

                outputBlob.Write(outBytes, 0, outBytes.Length);
            }
        }

        [FunctionName("TimerTest")]
        public static void TimerFunction([TimerTrigger("0 */1 * * * *")]TimerInfo timerInfo, ILogger log)
        {
            log.LogInformation($"TimerFunction: {DateTime.Now:g}");
        }
    }
}
