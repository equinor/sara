using api.MQTT;

namespace api.Services
{
    public interface IMqttMessageService
    {
        public void OnSaraVisualizationAvailable(SaraVisualizationAvailableMessage e);

        public void OnSaraAnalysisResultAvailable(SaraAnalysisResultMessage e);
    }

    public class MqttMessageService : IMqttMessageService
    {
        public MqttMessageService() { }

        public void OnSaraVisualizationAvailable(SaraVisualizationAvailableMessage e)
        {
            OnSaraVisualizationAvailableTriggered(e);
        }

        public void OnSaraAnalysisResultAvailable(SaraAnalysisResultMessage e)
        {
            OnSaraAnalysisResultAvailableTriggered(e);
        }

        public static event EventHandler<SaraVisualizationAvailableMessage>? MqttSaraVisualizationAvailable;
        public static event EventHandler<SaraAnalysisResultMessage>? MqttSaraAnalysisResultAvailable;

        protected virtual void OnSaraVisualizationAvailableTriggered(
            SaraVisualizationAvailableMessage e
        )
        {
            MqttSaraVisualizationAvailable?.Invoke(this, e);
        }

        public void OnSaraAnalysisResultAvailableTriggered(SaraAnalysisResultMessage e)
        {
            MqttSaraAnalysisResultAvailable?.Invoke(this, e);
        }
    }
}
