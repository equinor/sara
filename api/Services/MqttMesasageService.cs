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

    public class MqttMessageService(IPlantDataService PlantDataService) : IMqttMessageService
    {
        public async Task<PlantData> CreateFromMqttMessage(
            IsarInspectionResultMessage isarInspectionResultMessage
        )
        {
            var plantDataExists = await PlantDataService.ExistsByInspectionId(
                isarInspectionResultMessage.InspectionId
            );
            if (plantDataExists)
            {
                throw new InvalidOperationException(
                    $"Plant Data with inspection id {isarInspectionResultMessage.InspectionId} already exists"
                );
            }

            PlantData plantData = await PlantDataService.CreatePlantData(
                isarInspectionResultMessage.InspectionId,
                isarInspectionResultMessage.InstallationCode,
                isarInspectionResultMessage.TagID,
                isarInspectionResultMessage.InspectionDescription,
                isarInspectionResultMessage.InspectionDataPath.StorageAccount,
                isarInspectionResultMessage.InspectionDataPath.BlobContainer,
                isarInspectionResultMessage.InspectionDataPath.BlobName
            );
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
