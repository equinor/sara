using System.Text.Json.Serialization;
using api.Models;
namespace api.Controllers.Models
{
    public class AnalysisResponse
    {
        public AnalysisStatus Status { get; set; }

        [JsonConstructor]
#nullable disable
        public AnalysisResponse() { }
#nullable enable

        public AnalysisResponse(Analysis analysis)
        {
            Status = analysis.Status;
        }
    }
}
