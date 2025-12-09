using System.Text.Json.Serialization;
using api.Database.Models;

#pragma warning disable CS8618
#pragma warning disable IDE1006

namespace api.Controllers.Models
{
    public class PlantDataResponse
    {
        public string id { get; set; }

        [JsonConstructor]
        public PlantDataResponse() { }

        public PlantDataResponse(PlantData plantData)
        {
            id = plantData.Id;
        }
    }
}
