
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
using System.Threading.Tasks;
using System;
using System.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage.Blob;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Transforms;
using SixLabors.ImageSharp.Processing.Filters;

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
            if (DateTime.Now.IsDarkOutside())
            {
                log.LogInformation("Received motion but it's dark outside");
                return;
            }

            var motionConfiguration = new MotionConfiguration(context);
            var motionService = new MotionService(motionConfiguration, log);
            var motionDetectionResult = await motionService.DetectMotionAsync();
            
            await outputBlob.UploadFromByteArrayAsync(motionDetectionResult.ImageBytes, 0, motionDetectionResult.ImageBytes.Length);
            foreach (var mqttMessage in motionDetectionResult.MqttMessages)
            {
                outMessages.Add(mqttMessage);
            }
        }
    }
}
