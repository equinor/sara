using api.Database.Models;
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
        private IArgoWorkflowService ArgoWorkflowService =>
            _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<IArgoWorkflowService>();
        private ITimeseriesService TimeseriesService =>
            _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ITimeseriesService>();
        private IAnalysisMappingService AnalysisMappingService =>
            _scopeFactory
                .CreateScope()
                .ServiceProvider.GetRequiredService<IAnalysisMappingService>();

        public override void Subscribe()
        {
            MqttService.MqttIsarInspectionResultReceived += OnIsarInspectionResult;
            MqttService.MqttIsarInspectionValueReceived += OnIsarInspectionValue;
        }

        public override void Unsubscribe()
        {
            MqttService.MqttIsarInspectionResultReceived -= OnIsarInspectionResult;
            MqttService.MqttIsarInspectionValueReceived -= OnIsarInspectionValue;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await stoppingToken;
        }

        private async void OnIsarInspectionResult(object? sender, MqttReceivedArgs mqttArgs)
        {
            IsarInspectionResultMessage? isarInspectionResultMessage = null;
            try
            {
                isarInspectionResultMessage = (IsarInspectionResultMessage)mqttArgs.Message;
                _logger.LogInformation(
                    "Received ISAR inspection result message with InspectionId: {InspectionId}",
                    isarInspectionResultMessage.InspectionId
                );
                _logger.LogInformation(
                    "Received ISAR inspection result message with TagID: {TagID}",
                    isarInspectionResultMessage.TagID
                );
                _logger.LogInformation(
                    "Received ISAR inspection result message with InspectionDescription: {InspectionDescription}",
                    isarInspectionResultMessage.InspectionDescription
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

                var analysisToBeRun =
                    await AnalysisMappingService.GetAnalysisTypeFromInspectionDescriptionAndTag(
                        isarInspectionResultMessage.InspectionDescription,
                        isarInspectionResultMessage.TagID
                    );

                var shouldRunConstantLevelOiler = false;

                if (analysisToBeRun.Contains(AnalysisType.ConstantLevelOiler))
                {
                    _logger.LogInformation(
                        "Analysis type ConstantLevelOiler is set to be run for InspectionId: {InspectionId}",
                        isarInspectionResultMessage.InspectionId
                    );
                    shouldRunConstantLevelOiler = true;
                }
                var shouldRunFencilla = false;

                if (analysisToBeRun.Contains(AnalysisType.Fencilla))
                {
                    _logger.LogInformation(
                        "Analysis type Fencilla is set to be run for InspectionId: {InspectionId}",
                        isarInspectionResultMessage.InspectionId
                    );
                    shouldRunFencilla = true;
                }
                var shouldUploadToSTID = false;
                if (plantData.RawDataBlobStorageLocation.BlobName.EndsWith(".jpeg"))
                {
                    shouldUploadToSTID = true;
                    _logger.LogInformation(
                        "Raw data is a JPEG image, will upload to STID for InspectionId: {InspectionId}",
                        isarInspectionResultMessage.InspectionId
                    );
                }
                await ArgoWorkflowService.TriggerAnalysis(
                    plantData,
                    shouldRunConstantLevelOiler,
                    shouldRunFencilla,
                    shouldUploadToSTID
                );
            }
            catch (ArgumentException)
            {
                _logger.LogWarning(
                    "No analysis mapping found for InspectionId: {InspectionId}",
                    isarInspectionResultMessage?.InspectionId
                );
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(
                    ex,
                    "Error occurred while creating MQTT message from ISAR for InspectionId: {InspectionId}.",
                    isarInspectionResultMessage?.InspectionId
                );
            }
            catch (Exception ex)
            {
                if (isarInspectionResultMessage != null)
                {
                    _logger.LogError(
                        ex,
                        "Error occurred while triggering analysis workflow for InspectionId: {InspectionId}",
                        isarInspectionResultMessage.InspectionId
                    );
                }
                else
                {
                    _logger.LogError(
                        ex,
                        "Error occurred while triggering analysis workflow for unknown InspectionId"
                    );
                }
                return;
            }
        }

        private async void OnIsarInspectionValue(object? sender, MqttReceivedArgs mqttArgs)
        {
            try
            {
                var isarInspectionValueMessage = (IsarInspectionValueMessage)mqttArgs.Message;
                _logger.LogInformation(
                    "Received ISAR inspection value message with InspectionId: {InspectionId}",
                    isarInspectionValueMessage.InspectionId
                );
                await TimeseriesService.TriggerTimeseriesUpload(isarInspectionValueMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error occurred while processing ISAR inspection value message"
                );
            }
        }
    }
}
