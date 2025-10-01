using System.Text.Json.Serialization;
using api.Database.Models;

#pragma warning disable IDE1006

namespace api.Controllers.Models
{
    public class PlantDataResponse
    {
        public string id { get; set; }

        [JsonConstructor]
#nullable disable
        public PlantDataResponse() { }

#nullable enable

        public PlantDataResponse(PlantData plantData)
        {
            id = plantData.Id;
        }
    }

    public class StidDataResponse(
        string inspectionId,
        BlobStorageLocation anonymizedBlobStorageLocation,
        string tag,
        string description,
        WorkflowStatus stidWorkflowStatus
    )
    {
        public string inspectionId { get; set; } = inspectionId;
        public BlobStorageLocation anonymizedBlobStorageLocation { get; set; } =
            anonymizedBlobStorageLocation;
        public string tag { get; set; } = tag;
        public string description { get; set; } = description;
        public WorkflowStatus stidWorkflowStatus { get; set; } = stidWorkflowStatus;
    }
}
