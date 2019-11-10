using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;

namespace Functions
{
    public class MotionConfiguration
    {
        public string CamUrl { get; }
        public string Username { get; }
        public string Password { get; }
        public string PredictionKey { get; }
        public string PredictionEndpoint {get;}
        public string ProjectId { get; }
        public string ModelName { get; internal set; }

        public MotionConfiguration(ExecutionContext context)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            CamUrl = config["CamUrl"];
            Username = config["CamUsername"];
            Password = config["CamPassword"];
            PredictionKey = config["VisionApiPredictionKey"];
            ProjectId = config["VisionApiProjectId"];
            ModelName = config["ModelName"];
            PredictionEndpoint = config["VisionApiPredictionEndpoint"];
        }
    }
}
