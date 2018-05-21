using CaseOnline.Azure.WebJobs.Extensions.Mqtt.Messaging;
using System.Collections.Generic;

namespace Functions
{
    public class DetectMotionResult
    {
        public byte[] ImageBytes { get; set; }
        public IList<MqttMessage> MqttMessages { get; set; }

        public DetectMotionResult()
        {
            MqttMessages = new List<MqttMessage>();
        }
    }
}
