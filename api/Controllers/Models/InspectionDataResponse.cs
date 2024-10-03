using System.Text.Json.Serialization;
using api.Models;
namespace api.Controllers.Models
{
    public class InspectionDataResponse
    {
        public string Name { get; set; }

        [JsonConstructor]
#nullable disable
        public InspectionDataResponse() { }
#nullable enable

        public InspectionDataResponse(InspectionData inspectionData)
        {
            Name = inspectionData.Name;
        }
    }
}
