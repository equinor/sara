using System.Text.Json;
using System.Text.Json.Serialization;
using MQTTnet.Extensions.ManagedClient;

namespace api.MQTT
{
    public interface IMqttPublisherService
    {
        public Task PublishSaraVisualizationAvailable(
            SaraVisualizationAvailableMessage visualizationAvailableMessage
        );
        public Task PublishSaraAnalysisResultAvailable(
            SaraAnalysisResultMessage saraAnalysisResultMessage
        );
    }

    public class MqttPublisherService(ILogger<MqttService> logger, IManagedMqttClient mqttClient)
        : IMqttPublisherService
    {
        private readonly ILogger<MqttService> _logger = logger;

        private readonly IManagedMqttClient _mqttClient = mqttClient;

        private static readonly JsonSerializerOptions serializerOptions = new()
        {
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };

        public async Task PublishSaraVisualizationAvailable(
            SaraVisualizationAvailableMessage visualizationAvailableMessage
        )
        {
            var payload = JsonSerializer.Serialize(
                visualizationAvailableMessage,
                serializerOptions
            );
            var topic = "sara/visualization_available";
            await PublishAsync(topic, payload);
        }

        public async Task PublishSaraAnalysisResultAvailable(
            SaraAnalysisResultMessage analysisResultMessage
        )
        {
            var payload = JsonSerializer.Serialize(analysisResultMessage, serializerOptions);
            var topic = "sara/analysis_result_available";
            await PublishAsync(topic, payload);
        }

        private async Task PublishAsync(string topic, string payload)
        {
            _logger.LogInformation("Topic: {topic} - Payload to send: \n{payload}", topic, payload);

            try
            {
                await _mqttClient.EnqueueAsync(topic, payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    "Could not send MQTT message '{message}' object on topic '{topic}'. {exception}",
                    payload,
                    topic,
                    ex.Message
                );
                return;
            }
        }
    }
}
