#pragma warning disable CS8618
using api.Configurations;
using api.Database.Models;
using api.Services;

namespace api.Controllers.Models;

public class AnalysisDto
{
    public AnalysisDto(
        Analysis analysis,
        IBlobStorageService blobService,
        AnalysisOptions analysisOptions
    )
    {
        this.Id = analysis.Id;
        this.AnalysisType = analysis.AnalysisType;
        this.CreatedAt = analysis.CreatedAt;
        this.Runs = [.. analysis.Runs.Select(r => new AnalysisRunDto(r, blobService))];
        this.AnalysisGroup = analysis.AnalysisGroup;
        this.AnalysisGroupId = analysis.AnalysisGroupId;
        this.InspectionRecords = analysis.InspectionRecords;

        var workflows = analysis.Runs.SelectMany(r => r.Workflows);
        var workflowDtos = Runs.SelectMany(r => r.Workflows).ToDictionary(w => w.Id);

        var anonymizedWorkflow = workflows
            .Where(w => w.WorkflowType.Equals("anonymizer", StringComparison.OrdinalIgnoreCase))
            .Where(w => w.Status == WorkflowStatus.Succeeded)
            .OrderByDescending(w => w.CompletedAt ?? w.StartedAt ?? DateTime.MinValue)
            .FirstOrDefault();
        var anonymizedWorkflowDto = anonymizedWorkflow is null
            ? null
            : workflowDtos.GetValueOrDefault(anonymizedWorkflow.Id);
        if (anonymizedWorkflowDto is not null)
            this.AnonymizedSAS = anonymizedWorkflowDto.OutputBlobSAS;

        var analysisConfig = analysisOptions.Analyses[analysis.AnalysisType];
        var workflowChain = analysisConfig.Workflows;

        var lastWorkflowType = workflowChain.Last();

        var visualizedWorkflow = workflows
            .Where(w => !w.WorkflowType.Equals("anonymizer", StringComparison.OrdinalIgnoreCase))
            .Where(w => w.WorkflowType.Equals(lastWorkflowType, StringComparison.OrdinalIgnoreCase))
            .Where(w => w.Status == WorkflowStatus.Succeeded)
            .OrderByDescending(w => w.CompletedAt ?? w.StartedAt ?? DateTime.MinValue)
            .FirstOrDefault();
        var visualizedWorkflowDto = visualizedWorkflow is null
            ? null
            : workflowDtos.GetValueOrDefault(visualizedWorkflow.Id);
        if (visualizedWorkflowDto is not null)
        {
            this.VisualizedSAS = visualizedWorkflowDto.OutputBlobSAS;
            this.Result = visualizedWorkflowDto.Result;
        }
    }

    public Guid Id { get; set; }

    public string AnalysisType { get; set; }

    public DateTime CreatedAt { get; set; }

    public Uri? AnonymizedSAS { get; set; }

    public Uri? VisualizedSAS { get; set; }

    public Guid? AnalysisGroupId { get; set; }

    public AnalysisGroup? AnalysisGroup { get; set; }

    public List<InspectionRecord> InspectionRecords { get; set; }

    public List<AnalysisRunDto> Runs { get; set; } = [];

    public AnalysisResultDto? Result { get; set; }
}
