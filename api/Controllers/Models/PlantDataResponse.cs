using System.Text.Json.Serialization;
using api.Database.Models;

#pragma warning disable IDE1006

namespace api.Controllers.Models
{
    public class PlantDataResponse
    {
        public Guid id { get; set; }

        [JsonConstructor]
        public PlantDataResponse() { }

        public PlantDataResponse(PlantData plantData)
        {
            id = plantData.Id;
        }
    }
}
