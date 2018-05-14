
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

namespace Functions
{
    public static class Functions
    {
        [FunctionName("ReceiveSnapshotsFunction")]
        public static void ReceiveSnapshotsFunction(
            [MqttTrigger("dafang/dafang/motion/snapshot")]IMqttMessage snapshop,
            [Blob("snapshots/{sys.utcnow}.png", FileAccess.Write)] Stream outputBlob,
             ILogger log)
        {
            log.LogInformation("Receiving blob"); 

            var bytes = snapshop.GetMessage(); 
            outputBlob.Write(bytes,0, bytes.Length);
        }
    }
}
