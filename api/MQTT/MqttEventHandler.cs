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

        private IMqttMessageService MqttMessageService =>
            _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<IMqttMessageService>();
        private IArgoWorkflowService ArgoWorkflowService =>
            _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<IArgoWorkflowService>();
        private ITimeseriesService TimeseriesService =>
            _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ITimeseriesService>();

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
                "Received ISAR inspection result message with InspectionId: {InspectionId}, TagID: {TagID}, InspectionDescription: {InspectionDescription}",
                isarInspectionResultMessage.InspectionId,
                isarInspectionResultMessage.TagID,
                isarInspectionResultMessage.InspectionDescription
            );

            PlantData? plantData;
            try
            {
                plantData = await MqttMessageService.CreateFromMqttMessage(
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

            try
            {
                await ArgoWorkflowService.TriggerAnonymizer(
                    plantData.InspectionId,
                    plantData.Anonymization
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error occurred while triggering anonymizer workflow for InspectionId: {InspectionId}",
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
