using api.Database.Models;
using api.MQTT;

namespace api.Services
{
    public interface IMqttMessageService
    {
        public void OnSaraVisualizationAvailable(SaraVisualizationAvailableMessage e);

        public void OnSaraAnalysisResultAvailable(SaraAnalysisResultMessage e);

        public Task<PlantData> CreateFromMqttMessage(
            IsarInspectionResultMessage isarInspectionResultMessage
        );
    }

    public class MqttMessageService(
        ILogger<MqttMessageService> logger,
        IConfiguration configuration,
        IAnalysisMappingService AnalysisMappingService,
        IPlantDataService PlantDataService
    ) : IMqttMessageService
    {
        private static string PostfixAnalysisTypeToBlobName(
            string blobName,
            string analysisTypePostfix
        )
        {
            var blobNameComponents = blobName.Split(".");
            if (blobNameComponents.Length != 2)
            {
                throw new InvalidOperationException(
                    $"Invalid blobName, containing multiple dots: {blobName}"
                );
            }

            return blobNameComponents[0] + "_" + analysisTypePostfix + "." + blobNameComponents[1];
        }

        public async Task<PlantData> CreateFromMqttMessage(
            IsarInspectionResultMessage isarInspectionResultMessage
        )
        {
            var inspectionDataPath = isarInspectionResultMessage.InspectionDataPath;
            var rawStorageAccount = configuration.GetSection("Storage")["RawStorageAccount"];
            if (!inspectionDataPath.StorageAccount.Equals(rawStorageAccount))
            {
                throw new InvalidOperationException(
                    $"Incoming storage account, {inspectionDataPath.StorageAccount}, is not equal to storage account in config, {rawStorageAccount}."
                );
            }
            var rawDataBlobStorageLocation = new BlobStorageLocation
            {
                StorageAccount = inspectionDataPath.StorageAccount,
                BlobContainer = inspectionDataPath.BlobContainer,
                BlobName = inspectionDataPath.BlobName,
            };

            var anonymizedStorageAccount = configuration.GetSection("Storage")[
                "AnonStorageAccount"
            ];
            if (string.IsNullOrEmpty(anonymizedStorageAccount))
            {
                throw new InvalidOperationException("AnonStorageAccount is not configured.");
            }
            var anonymizedDataBlobStorageLocation = new BlobStorageLocation
            {
                StorageAccount = anonymizedStorageAccount,
                BlobContainer = inspectionDataPath.BlobContainer,
                BlobName = inspectionDataPath.BlobName,
            };

            List<AnalysisType> analysisToBeRun;
            try
            {
                analysisToBeRun =
                    await AnalysisMappingService.GetAnalysisTypeFromInspectionDescriptionAndTag(
                        isarInspectionResultMessage.InspectionDescription,
                        isarInspectionResultMessage.TagID
                    );
            }
            catch (Exception)
            {
                throw new InvalidOperationException(
                    "Error occurred while fetching analysis mapping"
                );
            }

            var visualizedStorageAccount = configuration.GetSection("Storage")["VisStorageAccount"];
            if (string.IsNullOrEmpty(visualizedStorageAccount))
            {
                throw new InvalidOperationException("VisStorageAccount is not configured.");
            }

            List<Workflow> Analyses = [];
            if (analysisToBeRun.Contains(AnalysisType.ConstantLevelOilerEstimator))
            {
                logger.LogInformation(
                    "Analysis type ConstantLevelOilerEstimator is set to be run for InspectionId: {InspectionId}",
                    isarInspectionResultMessage.InspectionId
                );
                var visualizedBlobStorageLocation = new BlobStorageLocation
                {
                    StorageAccount = visualizedStorageAccount,
                    BlobContainer = inspectionDataPath.BlobContainer,
                    BlobName = PostfixAnalysisTypeToBlobName(inspectionDataPath.BlobName, "cloe"),
                };
                Analyses.Add(
                    new CLOEAnalysis
                    {
                        SourceBlobStorageLocation = anonymizedDataBlobStorageLocation,
                        DestinationBlobStorageLocation = visualizedBlobStorageLocation,
                    }
                );
            }
            if (analysisToBeRun.Contains(AnalysisType.Fencilla))
            {
                logger.LogInformation(
                    "Analysis type Fencilla is set to be run for InspectionId: {InspectionId}",
                    isarInspectionResultMessage.InspectionId
                );
                var visualizedBlobStorageLocation = new BlobStorageLocation
                {
                    StorageAccount = visualizedStorageAccount,
                    BlobContainer = inspectionDataPath.BlobContainer,
                    BlobName = PostfixAnalysisTypeToBlobName(
                        inspectionDataPath.BlobName,
                        "fencilla"
                    ),
                };
                Analyses.Add(
                    new FencillaAnalysis
                    {
                        SourceBlobStorageLocation = anonymizedDataBlobStorageLocation,
                        DestinationBlobStorageLocation = visualizedBlobStorageLocation,
                    }
                );
            }

            var plantData = new PlantData
            {
                InspectionId = isarInspectionResultMessage.InspectionId,
                InstallationCode = isarInspectionResultMessage.InstallationCode,
                Anonymization = new Anonymization
                {
                    SourceBlobStorageLocation = rawDataBlobStorageLocation,
                    DestinationBlobStorageLocation = anonymizedDataBlobStorageLocation,
                },
            };
            await PlantDataService.WritePlantData(plantData);
            return plantData;
        }

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
