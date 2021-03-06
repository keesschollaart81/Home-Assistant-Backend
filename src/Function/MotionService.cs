
using System.IO;
using CaseOnline.Azure.WebJobs.Extensions.Mqtt.Messaging;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction;
using System.Linq;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction.Models;

namespace Functions
{
    public class MotionService
    {
        private readonly MotionConfiguration _config;
        private readonly ILogger _log;

        public MotionService(MotionConfiguration config, ILogger log)
        {
            _config = config;
            _log = log;
        }
        public async Task<DetectMotionResult> DetectMotionAsync(byte[] cameraImage)
        {
            var result = new DetectMotionResult();
            try
            {
                if (cameraImage.Length <= 0)
                {
                    return result;
                }

                var meaningfulpredictions = await GetMeaningfulpredictions(cameraImage);

                var mqttMessages = GetMqttMessagesForPredictions(meaningfulpredictions);

                result.MqttMessages = mqttMessages;
                result.ImageBytes = cameraImage;

                using (var image = Image.Load(cameraImage))
                using (var ms = new MemoryStream())
                {
                    image.Save(ms, new JpegEncoder()); // applies compression
                    result.ImageBytes = ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(new EventId(0), ex, $"Unhandled Exception while processing motion detection: {ex.Message}");
                throw;
            }
            return result;
        }

        private async Task<IEnumerable<PredictionModel>> GetMeaningfulpredictions(byte[] image)
        {
            using (var stream = new MemoryStream(image))
            {
                var client = new CustomVisionPredictionClient()
                {
                    ApiKey = _config.PredictionKey,
                    Endpoint = _config.PredictionEndpoint
                };
                var predictionResult = await client.DetectImageAsync(new Guid(_config.ProjectId), _config.ModelName, stream);

                foreach (var c in predictionResult.Predictions)
                {
                    _log.LogDebug($"\t{c.TagName}: {c.Probability:P1} [ {c.BoundingBox.Left}, {c.BoundingBox.Top}, {c.BoundingBox.Width}, {c.BoundingBox.Height} ]");
                }

                var meaningfulPredictions = predictionResult.Predictions.Where(x => x.Probability > 0.15);

                return meaningfulPredictions;
            }
        }

        private IList<MqttMessage> GetMqttMessagesForPredictions(IEnumerable<PredictionModel> predictions)
        {
            var result = new List<MqttMessage>();

            var doorPrediction = predictions.Where(x => x.TagName.Contains("door")).OrderByDescending(x => x.Probability).FirstOrDefault();
            var envelopeBodyDoor = doorPrediction == null ? "unkwown" : doorPrediction.TagName == "door-open" ? "open" : "closed";
            if (envelopeBodyDoor != "unkwown")
            {
                result.Add(new MqttMessage("motion/door", Encoding.UTF8.GetBytes(envelopeBodyDoor), MqttQualityOfServiceLevel.AtLeastOnce, true));
            }

            var gatePrediction = predictions.Where(x => x.TagName.Contains("gate")).OrderByDescending(x => x.Probability).FirstOrDefault();
            var envelopeBodyGate = gatePrediction == null ? "unkwown" : gatePrediction.TagName == "gate-open" ? "open" : "closed";
            if (envelopeBodyGate != "unkwown")
            {
                result.Add(new MqttMessage("motion/gate", Encoding.UTF8.GetBytes(envelopeBodyGate), MqttQualityOfServiceLevel.AtLeastOnce, true));
            }

            var bikeMarleen = predictions.Any(x => x.TagName == "bike-marleen");
            var envelopeBodyBikeMarleen = bikeMarleen ? "visible" : "not visible";
            result.Add(new MqttMessage("motion/bike/marleen", Encoding.UTF8.GetBytes(envelopeBodyBikeMarleen), MqttQualityOfServiceLevel.AtLeastOnce, true));

            var bikeJasmijn = predictions.Any(x => x.TagName == "bike-jasmijn");
            var envelopeBodyBikeJasmijn = bikeJasmijn ? "visible" : "not visible";
            result.Add(new MqttMessage("motion/bike/jasmijn", Encoding.UTF8.GetBytes(envelopeBodyBikeJasmijn), MqttQualityOfServiceLevel.AtLeastOnce, true));

            return result;
        }
    }
}
