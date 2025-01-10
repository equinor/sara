using api.MQTT;

namespace api.Services
{
    public interface IMqttMessageService
    {
        public void OnIdaVisualizationAvailable(IdaVisualizationAvailableMessage e);
    }

    public class MqttMessageService : IMqttMessageService
    {
        public MqttMessageService() { }

        public void OnIdaVisualizationAvailable(IdaVisualizationAvailableMessage e)
        {
            OnIdaVisualizationAvailableTriggered(e);
        }

        public static event EventHandler<IdaVisualizationAvailableMessage>? MqttIdaVisualizationAvailable;

        protected virtual void OnIdaVisualizationAvailableTriggered(IdaVisualizationAvailableMessage e)
        {
            MqttIdaVisualizationAvailable?.Invoke(this, e);
        }

    }
}
