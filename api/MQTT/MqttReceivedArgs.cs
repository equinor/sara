namespace api.MQTT;
public class MqttReceivedArgs(MqttMessage message) : EventArgs
{
    public MqttMessage Message { get; } = message;
}
