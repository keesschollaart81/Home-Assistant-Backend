
using System.IO;
using Microsoft.Azure.WebJobs;
using CaseOnline.Azure.WebJobs.Extensions.Mqtt.Messaging;
using CaseOnline.Azure.WebJobs.Extensions.Mqtt;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Functions
{
    public partial class Functions
    {
        [FunctionName(nameof(MotionFunction))]
        public async Task MotionFunction(
            [MqttTrigger("dafang/dafang/motion", ConnectionString = "MqttConnectionForMotion")]IMqttMessage snapshot,
            [Mqtt(ConnectionString = "MqttConnectionForMotion")] ICollector<IMqttMessage> outMessages,
            [Blob("motion/{sys.utcnow}.png", FileAccess.Write)] CloudBlockBlob outputBlob,
            ILogger log,
            ExecutionContext context)
        {
            await DoCameraAndMotionMagic(outMessages, outputBlob, log, context);
        }

        [FunctionName(nameof(HourlyMotionCheckFunction))]
        public async Task HourlyMotionCheckFunction([TimerTrigger("0 */15 * * * *")]TimerInfo timerInfo,
            [Mqtt(ConnectionString = "MqttConnectionForMotion")] ICollector<IMqttMessage> outMessages,
            [Blob("motion-hourly/{sys.utcnow}.png", FileAccess.Write)] CloudBlockBlob outputBlob,
            ILogger log,
            ExecutionContext context)
        {
            await DoCameraAndMotionMagic(outMessages, outputBlob, log, context);
        }

        public async Task DoCameraAndMotionMagic(
            ICollector<IMqttMessage> outMessages,
            CloudBlockBlob outputBlob,
            ILogger log,
            ExecutionContext context)
        {
            var motionConfiguration = new MotionConfiguration(context);
            var cameraService = new CameraService(motionConfiguration, log);
            var cameraStill = await cameraService.GetCameraStill();

            try
            {
                var motionService = new MotionService(motionConfiguration, log);
                var motionDetectionResult = await motionService.DetectMotionAsync(cameraStill);

                foreach (var mqttMessage in motionDetectionResult.MqttMessages)
                {
                    outMessages.Add(mqttMessage);
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error doing the AI stuff");
            }
            await outputBlob.UploadFromByteArrayAsync(cameraStill, 0, cameraStill.Length);
        }
    }
}
