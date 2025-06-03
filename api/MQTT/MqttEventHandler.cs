using api.Services;
using api.Utilities;

namespace api.MQTT
{
    /// <summary>
    ///     A background service which listens to events and performs callback functions.
    /// </summary>
    public class MqttEventHandler : EventHandlerBase
    {
        private readonly ILogger<MqttEventHandler> _logger;

        private readonly IServiceScopeFactory _scopeFactory;

        public MqttEventHandler(ILogger<MqttEventHandler> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            // Reason for using factory: https://www.thecodebuzz.com/using-dbcontext-instance-in-ihostedservice/
            _scopeFactory = scopeFactory;

            Subscribe();
        }

        private IPlantDataService PlantDataService =>
            _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<IPlantDataService>();
        private IAnonymizerService AnonymizerService =>
            _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<IAnonymizerService>();
        private ITimeseriesService TimeseriesService =>
            _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ITimeseriesService>();

        public override void Subscribe()
        {
            MqttService.MqttIsarInspectionResultReceived += OnIsarInspectionResult;
        }

        public override void Unsubscribe()
        {
            MqttService.MqttIsarInspectionResultReceived -= OnIsarInspectionResult;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await stoppingToken;
        }

        private async void OnIsarInspectionResult(object? sender, MqttReceivedArgs mqttArgs)
        {
            var isarInspectionResultMessage = (IsarInspectionResultMessage)mqttArgs.Message;
            _logger.LogInformation(
                "Received ISAR inspection result message with InspectionId: {InspectionId}",
                isarInspectionResultMessage.InspectionId
            );

            var existingPlantData = await PlantDataService.ReadByInspectionId(
                isarInspectionResultMessage.InspectionId
            );

            if (existingPlantData != null)
            {
                _logger.LogWarning(
                    "Plant Data with inspection id {InspectionId} already exists",
                    isarInspectionResultMessage.InspectionId
                );
                return;
            }

            var plantData = await PlantDataService.CreateFromMqttMessage(
                isarInspectionResultMessage
            );
            await AnonymizerService.TriggerAnonymizerFunc(plantData);
        }

        private async void OnIsarInspectionValue(object? sender, MqttReceivedArgs mqttArgs)
        {
            var isarInspectionValueMessage = (IsarInspectionValueMessage)mqttArgs.Message;
            _logger.LogInformation(
                "Received ISAR inspection value message with InspectionId: {InspectionId}",
                isarInspectionValueMessage.InspectionId
            );

            await TimeseriesService.TriggerTimeseriesUpload(isarInspectionValueMessage);
        }
    }
}
