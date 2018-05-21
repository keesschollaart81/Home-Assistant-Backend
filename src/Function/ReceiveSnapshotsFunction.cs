
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
using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage.Blob;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Transforms;
using SixLabors.ImageSharp.Processing.Filters;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace Functions
{
    public static partial class Functions
    {
        [FunctionName("MotionFunction")]
        public static async Task MotionFunction(
        [MqttTrigger("dafang/dafang/motion", ConnectionString = "MqttConnectionForMotion")]IMqttMessage snapshop,
        [Mqtt(ConnectionString = "MqttConnectionForMotion")] ICollector<IMqttMessage> outMessages,
        [Blob("motion/{sys.utcnow}.png", FileAccess.Write)] CloudBlockBlob outputBlob,
        ILogger log,
        ExecutionContext context)
        {
            var bytes = snapshop.GetMessage();
            var messageBody = Encoding.UTF8.GetString(bytes);
            var on = messageBody == "ON";

            if (!on)
            {
                log.LogInformation($"Message: {messageBody}");
                //await outputBlob.DeleteAsync();
                return;
            }

            var motionDetectionResult = await DetectMotionAsync(log, context);
            await outputBlob.UploadFromByteArrayAsync(motionDetectionResult.ImageBytes, 0, motionDetectionResult.ImageBytes.Length);
            foreach (var mqttMessage in motionDetectionResult.MqttMessages)
            {
                outMessages.Add(mqttMessage);
            }
        }

        private class DetectMotionResult
        {
            public byte[] ImageBytes { get; set; }
            public IList<MqttMessage> MqttMessages { get; set; }

            public DetectMotionResult()
            {
                MqttMessages = new List<MqttMessage>();
            }
        }
        private static async Task<DetectMotionResult> DetectMotionAsync(
            ILogger log,
            ExecutionContext context
             )
        {
            var result = new DetectMotionResult();
            try
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
                    var endpoint = new PredictionEndpoint() { ApiKey = predictionKey };
                    using (var stream = new MemoryStream(outBytes))
                    {
                        var predictionResult = endpoint.PredictImage(new Guid(projectId), stream);

                        foreach (var c in predictionResult.Predictions)
                        {
                            log.LogDebug($"\t{c.TagName}: {c.Probability:P1} [ {c.BoundingBox.Left}, {c.BoundingBox.Top}, {c.BoundingBox.Width}, {c.BoundingBox.Height} ]");
                        }

                        var meaningfulPredictions = predictionResult.Predictions.Where(x => x.Probability > 0.15);

                        var doorPrediction = meaningfulPredictions.Where(x => x.TagName.Contains("door")).OrderByDescending(x => x.Probability).FirstOrDefault();
                        var envelopeBodyDoor = doorPrediction == null ? "unkwown" : doorPrediction.TagName == "door-open" ? "open" : "closed";
                        result.MqttMessages.Add(new MqttMessage("motion/door", Encoding.UTF8.GetBytes(envelopeBodyDoor), MqttQualityOfServiceLevel.AtLeastOnce, true));

                        var gatePrediction = meaningfulPredictions.Where(x => x.TagName.Contains("gate")).OrderByDescending(x => x.Probability).FirstOrDefault();
                        var envelopeBodyGate = gatePrediction == null ? "unkwown" : gatePrediction.TagName == "gate-open" ? "open" : "closed";
                        result.MqttMessages.Add(new MqttMessage("motion/gate", Encoding.UTF8.GetBytes(envelopeBodyGate), MqttQualityOfServiceLevel.AtLeastOnce, true));

                        var bikeMarleen = meaningfulPredictions.Any(x => x.TagName == "bike-marleen");
                        var envelopeBodyBikeMarleen = bikeMarleen ? "visible" : "not visible";
                        result.MqttMessages.Add(new MqttMessage("motion/bike/marleen", Encoding.UTF8.GetBytes(envelopeBodyBikeMarleen), MqttQualityOfServiceLevel.AtLeastOnce, true));

                        var bikeJasmijn = meaningfulPredictions.Any(x => x.TagName == "bike-jasmijn");
                        var envelopeBodyBikeJasmijn = bikeJasmijn ? "visible" : "not visible";
                        result.MqttMessages.Add(new MqttMessage("motion/bike/jasmijn", Encoding.UTF8.GetBytes(envelopeBodyBikeJasmijn), MqttQualityOfServiceLevel.AtLeastOnce, true));
                    }
                    result.ImageBytes = outBytes;

                    try
                    {
                        using (var image = Image.Load(outBytes))
                        using (var ms = new MemoryStream())
                        {
                            image.Save(ms, new JpegEncoder()); // applies compression
                            result.ImageBytes = ms.ToArray();
                        }
                    }
                    catch (Exception ex)
                    {
                        log.LogWarning(new EventId(0), ex, "Image compression failed...");
                        // ignore while:
                        // https://github.com/SixLabors/ImageSharp/issues/574
                        // https://github.com/Azure/azure-functions-host/issues/2856
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogWarning(new EventId(0), ex, $"Unhandled Exception while processing motion detection: {ex.Message}");
                throw;
            }
            return result;
        }
    }
}
