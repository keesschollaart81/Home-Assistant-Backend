using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System;

namespace Functions
{
    public class CameraService
    {
        private readonly MotionConfiguration _config;
        private readonly ILogger _log;

        public CameraService(MotionConfiguration config, ILogger log)
        {
            _config = config;
            _log = log;
        }

        public async Task<byte[]> GetCameraStill()
        {
            _log.LogInformation($"Getting the camera's image...");

            using (var httpClientHandler = new HttpClientHandler())
            {
                httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; };
                using (var client = new HttpClient())
                {
                    var authHeaderValue = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes($"{_config.Username}:{_config.Password}"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeaderValue);

                    var response = await client.GetAsync(_config.CamUrl);
                    response.EnsureSuccessStatusCode();
                    var outBytes = await response.Content.ReadAsByteArrayAsync();

                    _log.LogInformation($"Blob received, size {outBytes.Length}");

                    return outBytes;
                }
            }
        }
    }
}
