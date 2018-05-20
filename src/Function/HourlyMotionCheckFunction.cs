
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

namespace Functions
{
    public static partial class Functions
    { 
        [FunctionName("HourlyMotionCheckFunction")]
        public static async Task HourlyMotionCheckFunction([TimerTrigger("0 0 * * * *")]TimerInfo timerInfo,
        [Mqtt(ConnectionString = "MqttConnectionForMotion")] ICollector<IMqttMessage> outMessages,
        [Blob("motion-hourly/{sys.utcnow}.png", FileAccess.Write)] Stream outputBlob,
        ILogger log,
        ExecutionContext context)
        {
            var motionDetectionResult = await DetectMotionAsync(log, context);
            outputBlob.Write(motionDetectionResult.ImageBytes, 0, motionDetectionResult.ImageBytes.Length);
            foreach (var mqttMessage in motionDetectionResult.MqttMessages)
            {
                outMessages.Add(mqttMessage);
            }
        } 
    }
}
