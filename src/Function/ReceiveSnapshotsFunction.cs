
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
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction;
using System.Linq;

namespace Functions
{
    public static class Functions
    {
        [FunctionName("MotionFunction")]
        public static async Task MotionFunction(
        [MqttTrigger("dafang/dafang/motion", ConnectionString = "MqttConnectionForMotion")]IMqttMessage snapshop,
        [Mqtt(ConnectionString = "MqttConnectionForMotion")] ICollector<IMqttMessage> outMessages,
        [Blob("snapshots/{sys.utcnow}.png", FileAccess.Write)] Stream outputBlob,
        ILogger log,
        ExecutionContext context)
        {

            var config = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var camUrl = config["CamUrl"];
            var username = config["CamUsername"];
            var password = config["CamPassword"];
            var predictionKey = config["VisionApiPredictionKey"];
            var projectId = config["VisionApiProjectId"]; 

            var bytes = snapshop.GetMessage();
            var messageBody = Encoding.UTF8.GetString(bytes);
            var on = messageBody == "ON"; 

            if (!on)
            {
                log.LogInformation($"Message: {messageBody}");

                outputBlob = null;
                return;
            }

            try
            {
                log.LogInformation($"Getting the camera's image...");

                var client = new HttpClient();
                var authHeaderValue = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes($"{username}:{password}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeaderValue);

                var response = await client.GetAsync(camUrl);
                response.EnsureSuccessStatusCode();
                var outBytes = await response.Content.ReadAsByteArrayAsync();

                log.LogInformation($"Blob received, size {outBytes.Length}");

                if (outBytes.Length > 0)
                {
                    outputBlob.Write(outBytes, 0, outBytes.Length);

                    var endpoint = new PredictionEndpoint() { ApiKey = predictionKey };
                    using (var stream = new MemoryStream(outBytes))
                    {
                        var result = endpoint.PredictImage(new Guid(projectId), stream);

                        foreach (var c in result.Predictions)
                        {
                            log.LogDebug($"\t{c.TagName}: {c.Probability:P1} [ {c.BoundingBox.Left}, {c.BoundingBox.Top}, {c.BoundingBox.Width}, {c.BoundingBox.Height} ]");
                        }

                        var meaningfulPredictions = result.Predictions.Where(x => x.Probability > 0.15);

                        var doorPrediction = meaningfulPredictions.Where(x => x.TagName.Contains("door")).OrderByDescending(x => x.Probability).FirstOrDefault();
                        var doorOpen = doorPrediction.TagName == "door-open";
                        var envelopeBodyDoor = doorOpen ? "open" : "closed";
                        outMessages.Add(new MqttMessage("motion/door", Encoding.UTF8.GetBytes(envelopeBodyDoor), MqttQualityOfServiceLevel.AtLeastOnce, true));

                        var gatePrediction = meaningfulPredictions.Where(x => x.TagName.Contains("gate")).OrderByDescending(x => x.Probability).FirstOrDefault();
                        var gateOpen = gatePrediction.TagName == "gate-open";
                        var envelopeBodyGate = gateOpen ? "open" : "closed";
                        outMessages.Add(new MqttMessage("motion/gate", Encoding.UTF8.GetBytes(envelopeBodyGate), MqttQualityOfServiceLevel.AtLeastOnce, true));

                        var bikeMarleen = meaningfulPredictions.Any(x => x.TagName == "bike-marleen");
                        var envelopeBodyBikeMarleen = bikeMarleen ? "visible" : "unvisible";
                        outMessages.Add(new MqttMessage("motion/bike/marleen", Encoding.UTF8.GetBytes(envelopeBodyBikeMarleen), MqttQualityOfServiceLevel.AtLeastOnce, true));

                        var bikeJasmijn = meaningfulPredictions.Any(x => x.TagName == "bike-jasmijn");
                        var envelopeBodyBikeJasmijn = bikeJasmijn ? "visible" : "unvisible";
                        outMessages.Add(new MqttMessage("motion/bike/jasmijn", Encoding.UTF8.GetBytes(envelopeBodyBikeJasmijn), MqttQualityOfServiceLevel.AtLeastOnce, true));
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogWarning(new EventId(0), ex, $"Unhandled Exception while processing motion detection: {ex.Message}");
            }
        }

        [FunctionName("TimerTest")]
        public static void TimerFunction([TimerTrigger("0 */1 * * * *")]TimerInfo timerInfo, ILogger log)
        {
            log.LogInformation($"TimerFunction: {DateTime.Now:g}");
        }
    }
}
