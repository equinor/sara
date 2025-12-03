using System.ComponentModel.DataAnnotations;
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

        private bool ValidateIsarInspectionResultMessage(IsarInspectionResultMessage message)
        {
            var validationResults = new List<ValidationResult>();
            bool isValid = Validator.TryValidateObject(
                message,
                new ValidationContext(message),
                validationResults,
                true
            );
            isValid &= Validator.TryValidateObject(
                message.InspectionDataPath,
                new ValidationContext(message.InspectionDataPath),
                validationResults,
                true
            );
            isValid &= Validator.TryValidateObject(
                message.InspectionMetadataPath,
                new ValidationContext(message.InspectionMetadataPath),
                validationResults,
                true
            );
            if (!isValid)
            {
                foreach (var validationResult in validationResults)
                {
                    _logger.LogError(
                        "Message validation error: {ErrorMessage}",
                        validationResult.ErrorMessage
                    );
                }
            }
            return isValid;
        }

        private async void OnIsarInspectionResult(object? sender, MqttReceivedArgs mqttArgs)
        {
            if (mqttArgs.Message is not IsarInspectionResultMessage isarInspectionResultMessage)
            {
                _logger.LogError(
                    "Received ISAR inspection result message is not of type IsarInspectionResultMessage"
                );
                return;
            }
            if (!ValidateIsarInspectionResultMessage(isarInspectionResultMessage))
            {
                _logger.LogError(
                    "Received ISAR inspection result message is invalid for InspectionId: {InspectionId}",
                    isarInspectionResultMessage.InspectionId
                );
                return;
            }

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

            PlantData? plantData;
            try
            {
                plantData = await PlantDataService.CreateFromMqttMessage(
                    isarInspectionResultMessage
                );
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(
                    ex,
                    "Error occurred while processing MQTT message from ISAR for InspectionId: {InspectionId}.",
                    isarInspectionResultMessage.InspectionId
                );
                return;
            }

            var shouldRunConstantLevelOiler = false;
            var shouldRunFencilla = false;
            var shouldRunSteamTrap = false;
            try
            {
                var analysisToBeRun =
                    await AnalysisMappingService.GetAnalysisTypeFromInspectionDescriptionAndTag(
                        isarInspectionResultMessage.InspectionDescription,
                        isarInspectionResultMessage.TagID
                    );
                if (analysisToBeRun.Contains(AnalysisType.ConstantLevelOiler))
                {
                    _logger.LogInformation(
                        "Analysis type ConstantLevelOiler is set to be run for InspectionId: {InspectionId}",
                        isarInspectionResultMessage.InspectionId
                    );
                    shouldRunConstantLevelOiler = true;
                }
                if (analysisToBeRun.Contains(AnalysisType.Fencilla))
                {
                    _logger.LogInformation(
                        "Analysis type Fencilla is set to be run for InspectionId: {InspectionId}",
                        isarInspectionResultMessage.InspectionId
                    );
                    shouldRunFencilla = true;
                }
                if (analysisToBeRun.Contains(AnalysisType.SteamTrap))
                {
                    _logger.LogInformation(
                        "Analysis type SteamTrap is set to be run for InspectionId: {InspectionId}",
                        isarInspectionResultMessage.InspectionId
                    );
                    shouldRunSteamTrap = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error occurred while fetching analysis mapping for InspectionId: {InspectionId}",
                    isarInspectionResultMessage.InspectionId
                );
                return;
            }

            try
            {
                await ArgoWorkflowService.TriggerAnalysis(
                    plantData,
                    shouldRunConstantLevelOiler,
                    shouldRunFencilla,
                    shouldRunSteamTrap
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error occurred while triggering workflow for InspectionId: {InspectionId}",
                    isarInspectionResultMessage.InspectionId
                );
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
