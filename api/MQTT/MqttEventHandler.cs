using System.ComponentModel.DataAnnotations;
using api.Database.Models;
using api.Services;
using api.Utilities;
using Microsoft.EntityFrameworkCore;

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
            if (message.AcousticMetadata is not null)
            {
                isValid &= Validator.TryValidateObject(
                    message.AcousticMetadata,
                    new ValidationContext(message.AcousticMetadata),
                    validationResults,
                    true
                );
            }
            if (message.AnalysisGroup is not null)
            {
                isValid &= Validator.TryValidateObject(
                    message.AnalysisGroup,
                    new ValidationContext(message.AnalysisGroup),
                    validationResults,
                    true
                );
            }
            if (message.RobotPose is not null)
            {
                isValid &= Validator.TryValidateObject(
                    message.RobotPose,
                    new ValidationContext(message.RobotPose),
                    validationResults,
                    true
                );
                if (message.RobotPose.Position is not null)
                {
                    isValid &= Validator.TryValidateObject(
                        message.RobotPose.Position,
                        new ValidationContext(message.RobotPose.Position),
                        validationResults,
                        true
                    );
                }
                if (message.RobotPose.Orientation is not null)
                {
                    isValid &= Validator.TryValidateObject(
                        message.RobotPose.Orientation,
                        new ValidationContext(message.RobotPose.Orientation),
                        validationResults,
                        true
                    );
                }
            }
            if (message.TargetPosition is not null)
            {
                isValid &= Validator.TryValidateObject(
                    message.TargetPosition,
                    new ValidationContext(message.TargetPosition),
                    validationResults,
                    true
                );
            }
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
            await ProcessIsarInspectionResult(isarInspectionResultMessage);
        }

        /// <summary>
        /// Processes a validated ISAR inspection result message: creates the
        /// inspection record and triggers analyses. Exposed as <c>internal</c>
        /// so integration tests can drive the pipeline deterministically
        /// without relying on the MQTT static event.
        /// </summary>
        internal async Task ProcessIsarInspectionResult(
            IsarInspectionResultMessage isarInspectionResultMessage
        )
        {
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
                isarInspectionResultMessage.TagId,
                isarInspectionResultMessage.InspectionDescription
            );

            var inspectionDataPath = isarInspectionResultMessage.InspectionDataPath;
            using var scope = _scopeFactory.CreateScope();
            var blobStorageLocation = new BlobStorageLocation
            {
                StorageAccount = inspectionDataPath.StorageAccount,
                BlobContainer = inspectionDataPath.BlobContainer,
                BlobName = inspectionDataPath.BlobName,
            };

            bool blobExists;
            try
            {
                blobExists = await scope
                    .ServiceProvider.GetRequiredService<IBlobStorageService>()
                    .ExistsAsync(blobStorageLocation);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to verify blob existence for ISAR inspection result. InspectionId: {InspectionId}, "
                        + "BlobPath: {StorageAccount}/{BlobContainer}/{BlobName}, ErrorMessage: {ErrorMessage}.",
                    isarInspectionResultMessage.InspectionId,
                    inspectionDataPath.StorageAccount,
                    inspectionDataPath.BlobContainer,
                    inspectionDataPath.BlobName,
                    ex.Message
                );
                return;
            }

            if (!blobExists)
            {
                _logger.LogError(
                    "Blob location referenced by ISAR inspection result does not exist. No inspection record "
                        + "or analysis will be created. InspectionId: {InspectionId}, "
                        + "BlobPath: {StorageAccount}/{BlobContainer}/{BlobName}.",
                    isarInspectionResultMessage.InspectionId,
                    inspectionDataPath.StorageAccount,
                    inspectionDataPath.BlobContainer,
                    inspectionDataPath.BlobName
                );
                return;
            }

            MqttInspectionRecordResult result;
            try
            {
                result = await scope
                    .ServiceProvider.GetRequiredService<IInspectionRecordService>()
                    .CreateFromMqttMessage(isarInspectionResultMessage);
            }
            catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException)
            {
                _logger.LogError(
                    ex,
                    "Error occurred while processing MQTT message from ISAR for InspectionId: {InspectionId}. "
                        + "TagID: {TagID}, InstallationCode: {InstallationCode}, RobotName: {RobotName}, "
                        + "RawBlobPath: {RawStorageAccount}/{RawBlobContainer}/{RawBlobName}, ErrorMessage: {ErrorMessage}, "
                        + "InnerErrorMessage: {InnerErrorMessage}.",
                    isarInspectionResultMessage.InspectionId,
                    isarInspectionResultMessage.TagId,
                    isarInspectionResultMessage.InstallationCode,
                    isarInspectionResultMessage.RobotName,
                    isarInspectionResultMessage.InspectionDataPath.StorageAccount,
                    isarInspectionResultMessage.InspectionDataPath.BlobContainer,
                    isarInspectionResultMessage.InspectionDataPath.BlobName,
                    ex.Message,
                    ex.InnerException?.Message
                );
                return;
            }

            if (result.IsDuplicate)
            {
                _logger.LogInformation(
                    "Ignoring duplicate ISAR inspection result for InspectionId: {InspectionId}, RecordId: {RecordId}.",
                    result.Record.InspectionId,
                    result.Record.Id
                );
                return;
            }

            try
            {
                await scope
                    .ServiceProvider.GetRequiredService<IAnalysisTriggerService>()
                    .OnInspectionRecordCreated(result.Record);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error occurred while triggering analyses for InspectionId: {InspectionId}",
                    isarInspectionResultMessage.InspectionId
                );
                return;
            }
        }

        private async void OnIsarInspectionValue(object? sender, MqttReceivedArgs mqttArgs)
        {
            if (mqttArgs.Message is not IsarInspectionValueMessage message)
            {
                _logger.LogError("Received ISAR inspection value message has an unexpected type");
                return;
            }

            await ProcessIsarInspectionValue(message);
        }

        internal async Task ProcessIsarInspectionValue(IsarInspectionValueMessage message)
        {
            using var scope = _scopeFactory.CreateScope();
            MqttInspectionRecordResult result;
            try
            {
                ValidateInspectionValue(message);
                await VerifyThatBlobExists(scope, message.InspectionDataPath);

                result = await scope
                    .ServiceProvider.GetRequiredService<IInspectionRecordService>()
                    .CreateFromMqttMessage(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to persist ISAR inspection value for InspectionId: {InspectionId}. Timeseries forwarding is skipped.",
                    message.InspectionId
                );
                return;
            }

            if (!result.IsDuplicate)
            {
                try
                {
                    await scope
                        .ServiceProvider.GetRequiredService<IMqttPublisherService>()
                        .PublishSaraInspectionRecordAvailable(
                            new SaraInspectionRecordAvailableMessage
                            {
                                InspectionId = result.Record.InspectionId,
                            }
                        );
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to publish inspection record availability for InspectionId: {InspectionId}",
                        result.Record.InspectionId
                    );
                }
            }

            try
            {
                _logger.LogInformation(
                    "Received ISAR inspection value message with InspectionId: {InspectionId}",
                    message.InspectionId
                );
                var name = CreateTimeseriesNameFromMQTT(message);
                var uploadRequest = new TriggerTimeseriesUploadRequest
                {
                    Name = name,
                    Facility = message.InstallationCode,
                    ExternalId = "",
                    Description = message.InspectionType,
                    Unit = message.Unit,
                    AssetId = message.InstallationCode, // TODO: check what assetId is
                    Value = message.Value,
                    Timestamp = message.Timestamp,
                    Metadata = new Dictionary<string, string>
                    {
                        { "tag_id", message.TagID },
                        { "inspection_description", message.InspectionDescription },
                        { "robot_name", message.RobotName },
                    },
                };
                await scope
                    .ServiceProvider.GetRequiredService<ITimeseriesService>()
                    .TriggerTimeseriesUpload(uploadRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to forward ISAR inspection value to timeseries for InspectionId: {InspectionId}",
                    message.InspectionId
                );
            }

            if (result.IsDuplicate)
            {
                _logger.LogInformation(
                    "Ignoring duplicate ISAR inspection value for analysis triggering. InspectionId: {InspectionId}, RecordId: {RecordId}",
                    result.Record.InspectionId,
                    result.Record.Id
                );
                return;
            }

            try
            {
                await scope
                    .ServiceProvider.GetRequiredService<IAnalysisTriggerService>()
                    .OnInspectionRecordCreated(result.Record);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error occurred while triggering analyses for InspectionId: {InspectionId}",
                    message.InspectionId
                );
            }
        }

        private static void ValidateInspectionValue(IsarInspectionValueMessage message)
        {
            if (
                string.IsNullOrWhiteSpace(message.InspectionId)
                || string.IsNullOrWhiteSpace(Sanitize.SanitizeUserInput(message.InspectionId))
                || string.IsNullOrWhiteSpace(message.InstallationCode)
                || string.IsNullOrWhiteSpace(message.InspectionType)
            )
                throw new ValidationException(
                    "InspectionId, InstallationCode and InspectionType are required."
                );

            var path = message.InspectionDataPath;
            if (path is null)
                throw new ValidationException(
                    "blob_storage_data_path is required for inspection values."
                );
            Validator.ValidateObject(path, new ValidationContext(path), true);
            if (message.RobotPose is { } robotPose)
                Validator.ValidateObject(robotPose, new ValidationContext(robotPose), true);
            if (message.TargetPosition is { } targetPosition)
                Validator.ValidateObject(
                    targetPosition,
                    new ValidationContext(targetPosition),
                    true
                );
        }

        private static async Task VerifyThatBlobExists(
            IServiceScope scope,
            InspectionPathMessage path
        )
        {
            var location = new BlobStorageLocation
            {
                StorageAccount = path.StorageAccount,
                BlobContainer = path.BlobContainer,
                BlobName = path.BlobName,
            };
            var blobExists = await scope
                .ServiceProvider.GetRequiredService<IBlobStorageService>()
                .ExistsAsync(location);
            if (!blobExists)
                throw new InvalidOperationException(
                    $"Blob referenced by ISAR inspection value does not exist: {location}."
                );
        }

        private static string CreateTimeseriesNameFromMQTT(
            IsarInspectionValueMessage isarInspectionValueMessage
        )
        {
            string description =
                isarInspectionValueMessage.InspectionDescription?.Replace(" ", "-") ?? string.Empty;
            var name =
                $"{isarInspectionValueMessage.InstallationCode}_"
                + $"{FloorWithTolerance(isarInspectionValueMessage.X)}E_"
                + $"{FloorWithTolerance(isarInspectionValueMessage.Y)}N_"
                + $"{FloorWithTolerance(isarInspectionValueMessage.Z)}U_"
                + $"{isarInspectionValueMessage.TagID}_"
                + $"{isarInspectionValueMessage.RobotName}_"
                + $"{description}";
            return name;
        }

        // Tolerance set to 0.06 by default to mimic expected fault tolerance in a robot positioning system
        public static int FloorWithTolerance(double value, double tolerance = 0.06)
        {
            var floored = (int)Math.Floor(value);
            if (value - floored >= 1 - tolerance)
                return floored + 1;
            return floored;
        }
    }
}
