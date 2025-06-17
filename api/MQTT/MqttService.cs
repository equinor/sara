using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using api.Services;
using api.Utilities;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Packets;
using MQTTnet.Protocol;

namespace api.MQTT
{
    public class MqttService : BackgroundService
    {
        private readonly ILogger<MqttService> _logger;
        private readonly int _maxRetryAttempts;

        private readonly IManagedMqttClient _mqttClient;

        private readonly ManagedMqttClientOptions _options;

        private readonly TimeSpan _reconnectDelay = TimeSpan.FromSeconds(5);

        private readonly string _serverHost;
        private readonly int _serverPort;
        private readonly bool _shouldFailOnMaxRetries;

        private static readonly JsonSerializerOptions serializerOptions = new()
        {
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };

        private CancellationToken _cancellationToken;
        private int _reconnectAttempts;

        public MqttService(ILogger<MqttService> logger, IConfiguration config)
        {
            _reconnectAttempts = 0;
            _logger = logger;
            var mqttFactory = new MqttFactory();
            _mqttClient = mqttFactory.CreateManagedMqttClient();

            var mqttConfig = config.GetSection("Mqtt");
            string password = mqttConfig.GetValue<string>("Password") ?? "";
            string username = mqttConfig.GetValue<string>("Username") ?? "";
            _serverHost = mqttConfig.GetValue<string>("Host") ?? "";
            _serverPort = mqttConfig.GetValue<int>("Port");
            _maxRetryAttempts = mqttConfig.GetValue<int>("MaxRetryAttempts");
            _shouldFailOnMaxRetries = mqttConfig.GetValue<bool>("ShouldFailOnMaxRetries");

            var tlsOptions = new MqttClientTlsOptions
            {
                UseTls = true,
                /* Currently disabled to use self-signed certificate in the internal broker communication */
                //if (_notProduction)
                IgnoreCertificateChainErrors = true,
            };
            var builder = new MqttClientOptionsBuilder()
                .WithTcpServer(_serverHost, _serverPort)
                .WithTlsOptions(tlsOptions)
                .WithCredentials(username, password);

            _options = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(_reconnectDelay)
                .WithClientOptions(builder.Build())
                .Build();

            RegisterCallbacks();

            var topics = mqttConfig.GetSection("Topics").Get<List<string>>() ?? [];
            SubscribeToTopics(topics);

            Subscribe();
        }

        public static event EventHandler<MqttReceivedArgs>? MqttIsarInspectionResultReceived;
        public static event EventHandler<MqttReceivedArgs>? MqttIsarInspectionValueReceived;

        public void Subscribe()
        {
            MqttMessageService.MqttSaraVisualizationAvailable += OnSaraVisualizationAvailable;
        }

        public void Unubscribe()
        {
            MqttMessageService.MqttSaraVisualizationAvailable -= OnSaraVisualizationAvailable;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _cancellationToken = stoppingToken;
            _logger.LogInformation("MQTT client STARTED");
            await _mqttClient.StartAsync(_options);
            await _cancellationToken;
            await _mqttClient.StopAsync();
            _logger.LogInformation("MQTT client STOPPED");
        }

        /// <summary>
        ///     The callback function for when a subscribed topic publishes a message
        /// </summary>
        /// <param name="messageReceivedEvent"> The event information for the MQTT message </param>
        private Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs messageReceivedEvent)
        {
            string content = messageReceivedEvent.ApplicationMessage.ConvertPayloadToString();
            string topic = messageReceivedEvent.ApplicationMessage.Topic;

            var messageType = MqttTopics.TopicsToMessages.GetItemByTopic(topic);
            if (messageType is null)
            {
                _logger.LogError("No message class defined for topic '{topicName}'", topic);
                return Task.CompletedTask;
            }

            _logger.LogDebug("Topic: {topic} - Message received: \n{payload}", topic, content);

            switch (messageType)
            {
                case Type type when type == typeof(IsarInspectionResultMessage):
                    OnIsarTopicReceived<IsarInspectionResultMessage>(content);
                    break;
                case Type type when type == typeof(IsarInspectionValueMessage):
                    OnIsarTopicReceived<IsarInspectionValueMessage>(content);
                    break;
                default:
                    _logger.LogWarning(
                        "No callback defined for MQTT message type '{type}'",
                        messageType.Name
                    );
                    break;
            }

            return Task.CompletedTask;
        }

        private async void OnSaraVisualizationAvailable(object? sender, MqttMessage e)
        {
            if (e is not SaraVisualizationAvailableMessage message)
            {
                _logger.LogError("Message is not of type SaraVisualizationAvailableMessage");
                return;
            }
            var payload = JsonSerializer.Serialize(message, serializerOptions);

            var topic = MqttTopics.MessagesToTopics.GetTopicByItem(message.GetType());
            if (topic is null)
            {
                _logger.LogError(
                    "No topic class defined for message of type '{messageType}'",
                    message.GetType().Name
                );
                return;
            }

            _logger.LogDebug("Topic: {topic} - Message to send: \n{payload}", topic, payload);

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

        private Task OnMessagePublished(ApplicationMessageProcessedEventArgs messageProcessedEvent)
        {
            string content =
                messageProcessedEvent.ApplicationMessage?.ApplicationMessage?.ConvertPayloadToString()
                ?? string.Empty;

            _logger.LogInformation("Message sent: \n{payload}", content);

            return Task.CompletedTask;
        }

        private Task OnConnected(MqttClientConnectedEventArgs obj)
        {
            _logger.LogInformation(
                "Successfully connected to broker at {host}:{port}.",
                _serverHost,
                _serverPort
            );
            _reconnectAttempts = 0;

            return Task.CompletedTask;
        }

        private Task OnConnectingFailed(ConnectingFailedEventArgs obj)
        {
            if (_reconnectAttempts == -1)
            {
                return Task.CompletedTask;
            }

            string errorMsg =
                "Failed to connect to MQTT broker. Exception: " + obj.Exception.Message;

            if (_reconnectAttempts >= _maxRetryAttempts)
            {
                _logger.LogError("{errorMsg}\n      Exceeded max reconnect attempts.", errorMsg);

                if (_shouldFailOnMaxRetries)
                {
                    _logger.LogError("Stopping MQTT client due to critical failure");
                    StopAsync(_cancellationToken);
                    return Task.CompletedTask;
                }

                _reconnectAttempts = -1;
                return Task.CompletedTask;
            }

            _reconnectAttempts++;
            _logger.LogWarning(
                "{errorMsg}\n      Retrying in {time}s ({attempt}/{maxAttempts})",
                errorMsg,
                _reconnectDelay.Seconds,
                _reconnectAttempts,
                _maxRetryAttempts
            );
            return Task.CompletedTask;
        }

        private Task OnDisconnected(MqttClientDisconnectedEventArgs obj)
        {
            // Only log a disconnect if previously connected (not on reconnect attempt)
            if (obj.ClientWasConnected)
            {
                if (obj.Reason is MqttClientDisconnectReason.NormalDisconnection)
                {
                    _logger.LogInformation(
                        "Successfully disconnected from broker at {host}:{port}",
                        _serverHost,
                        _serverPort
                    );
                }
                else
                {
                    _logger.LogWarning(
                        "Lost connection to broker at {host}:{port}",
                        _serverHost,
                        _serverPort
                    );
                }
            }

            return Task.CompletedTask;
        }

        private void RegisterCallbacks()
        {
            _mqttClient.ConnectedAsync += OnConnected;
            _mqttClient.DisconnectedAsync += OnDisconnected;
            _mqttClient.ConnectingFailedAsync += OnConnectingFailed;
            _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceived;
            _mqttClient.ApplicationMessageProcessedAsync += OnMessagePublished;
        }

        public void SubscribeToTopics(List<string> topics)
        {
            List<MqttTopicFilter> topicFilters = [];
            StringBuilder sb = new();
            sb.AppendLine("Mqtt service subscribing to the following topics:");
            topics.ForEach(topic =>
            {
                topicFilters.Add(new MqttTopicFilter { Topic = topic });
                sb.AppendLine(topic);
            });
            _logger.LogInformation("{topicContent}", sb.ToString());
            _mqttClient.SubscribeAsync(topicFilters).Wait();
        }

        private void OnIsarTopicReceived<T>(string content)
            where T : MqttMessage
        {
            T? message;

            try
            {
                message = JsonSerializer.Deserialize<T>(content, serializerOptions);
                if (message is null)
                {
                    throw new JsonException();
                }
            }
            catch (Exception ex)
                when (ex is JsonException or NotSupportedException or ArgumentException)
            {
                _logger.LogError(
                    ex,
                    "Could not create '{className}' object from MQTT message json. The content was the following: {content}",
                    typeof(T).Name,
                    content
                );
                return;
            }

            var type = typeof(T);
            try
            {
                var raiseEvent = type switch
                {
                    var t when t == typeof(IsarInspectionResultMessage) =>
                        MqttIsarInspectionResultReceived,
                    var t when t == typeof(IsarInspectionValueMessage) =>
                        MqttIsarInspectionValueReceived,
                    _ => throw new NotImplementedException(
                        $"No event defined for message type '{typeof(T).Name}'"
                    ),
                };
                // Event will be null if there are no subscribers
                if (raiseEvent is not null)
                {
                    raiseEvent(this, new MqttReceivedArgs(message));
                }
            }
            catch (NotImplementedException e)
            {
                _logger.LogWarning("{msg}", e.Message);
            }
        }
    }
}
