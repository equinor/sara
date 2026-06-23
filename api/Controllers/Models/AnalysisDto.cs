#pragma warning disable CS8618
using api.Database.Models;
using api.Services;

namespace api.Controllers.Models;

public class AnalysisDto
{
    public AnalysisDto(Analysis analysis, IBlobStorageService blobService)
    {
        this.Id = analysis.Id;
        this.Name = analysis.Name;
        this.CreatedAt = analysis.CreatedAt;

        var workflows = analysis.Runs.SelectMany(r => r.Workflows);

        var anonymizedWorkflow = workflows
            .Where(w => w.WorkflowType.Equals("anonymizer", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(w => w.CompletedAt ?? w.StartedAt ?? DateTime.MinValue)
            .FirstOrDefault();
        if (anonymizedWorkflow != null && anonymizedWorkflow.OutputBlobStorageLocation != null)
            this.AnonymizedSAS = blobService
                .CreateUserDelegationSASUri(anonymizedWorkflow.OutputBlobStorageLocation)
                .Result;

        var visualizedWorkflow = workflows
            .Where(w => !w.WorkflowType.Equals("anonymizer", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(w => w.CompletedAt ?? w.StartedAt ?? DateTime.MinValue)
            .FirstOrDefault();
        if (visualizedWorkflow != null && visualizedWorkflow.OutputBlobStorageLocation != null)
            this.VisualizedSAS = blobService
                .CreateUserDelegationSASUri(visualizedWorkflow.OutputBlobStorageLocation)
                .Result;
    }

    public Guid Id { get; set; }

    public string Name { get; set; }

    public DateTime CreatedAt { get; set; }

    public Uri? AnonymizedSAS { get; set; }

    public Uri? VisualizedSAS { get; set; }
}
