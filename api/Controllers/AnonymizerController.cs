using api.Controllers.Models;
using api.Services;
using api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace api.Controllers;

public class TriggerAnonymizerRequest
{
    public required string InspectionId { get; set; }
    public required Uri RawDataUri { get; set; }
    public required Uri AnonymizedUri { get; set; }
}


[ApiController]
[Route("[controller]")]
public class AnonymizerController(AnonymizerService anonymizerService, IdaDbContext dbContext) : ControllerBase
{
    private readonly AnonymizerService anonymizerService = anonymizerService;
    private readonly IdaDbContext dbContext = dbContext;

    /// <summary>
    /// Triggers the anonymizer workflow. NB: Anonymizer workflow should normally be triggered by MQTT message
    /// </summary>
    [HttpPost]
    [Route("trigger-anonymizer")]
    [Authorize(Roles = Role.Any)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TriggerAnonymizer([FromBody] TriggerAnonymizerRequest request)
    {
        var inspectionData = new InspectionData
        {
            Id = Guid.NewGuid().ToString(),
            InspectionId = request.InspectionId,
            RawDataUri = request.RawDataUri,
            AnonymizedUri = request.AnonymizedUri,
            DateCreated = DateTime.UtcNow,
            AnonymizerWorkflowStatus = WorkflowStatus.NotStarted,
            AnalysisToBeRun = [],
            Analysis = []
        };

        dbContext.InspectionData.Add(inspectionData);
        await dbContext.SaveChangesAsync();

        await anonymizerService.TriggerAnonymizerFunc(inspectionData);

        return Ok("Anonymizer workflow triggered successfully.");
    }
}
