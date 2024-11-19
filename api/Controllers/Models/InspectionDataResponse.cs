using System.Text.Json.Serialization;
using api.Models;
namespace api.Controllers.Models
{
    public class InspectionDataResponse
    {
        public string Id { get; set; }

        [JsonConstructor]
#nullable disable
        public InspectionDataResponse() { }
#nullable enable

        public InspectionDataResponse(InspectionData inspectionData)
        {
            Id = inspectionData.Id;
        }
    }
}
