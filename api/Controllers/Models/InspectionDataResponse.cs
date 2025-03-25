using System.Text.Json.Serialization;
using api.Database;

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
}
