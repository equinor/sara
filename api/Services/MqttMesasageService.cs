using api.MQTT;

namespace api.Services
{
    public interface IMqttMessageService
    {
        public void OnSaraVisualizationAvailable(SaraVisualizationAvailableMessage e);
    }

    public class MqttMessageService : IMqttMessageService
    {
        public MqttMessageService() { }

        public void OnSaraVisualizationAvailable(SaraVisualizationAvailableMessage e)
        {
            OnSaraVisualizationAvailableTriggered(e);
        }

        public static event EventHandler<SaraVisualizationAvailableMessage>? MqttSaraVisualizationAvailable;

        protected virtual void OnSaraVisualizationAvailableTriggered(
            SaraVisualizationAvailableMessage e
        )
        {
            MqttSaraVisualizationAvailable?.Invoke(this, e);
        }
    }
}
