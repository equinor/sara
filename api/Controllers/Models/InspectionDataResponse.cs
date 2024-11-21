using System.Text.Json.Serialization;
using api.Models;

#pragma warning disable IDE1006

namespace api.Controllers.Models
{
    public class InspectionDataResponse
    {
        public string id { get; set; }

        [JsonConstructor]
#nullable disable
        public InspectionDataResponse() { }
#nullable enable

        public InspectionDataResponse(InspectionData inspectionData)
        {
            id = inspectionData.Id;
        }
    }
}
