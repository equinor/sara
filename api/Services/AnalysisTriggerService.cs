using api.Configurations;
using api.Database.Context;
using api.Database.Models;
using api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace api.Services;

public interface IAnalysisTriggerService
{
    public Task OnInspectionRecordCreated(InspectionRecord inspectionRecord);

    public Task RerunAnalysis(Guid analysisId);
}

public class AnalysisTriggerService(
    SaraDbContext context,
    IOptions<AnalysisOptions> analysisOptions,
    IWorkflowService workflowService,
    ILogger<AnalysisTriggerService> logger
) : IAnalysisTriggerService
{
    private readonly AnalysisOptions _options = analysisOptions.Value;

    public async Task OnInspectionRecordCreated(InspectionRecord inspectionRecord)
    {
        var analysisTypes = inspectionRecord.Analyses.Select((a) => a.AnalysisType).ToList();
        if (analysisTypes.Count == 0)
        {
            return;
        }

        AnalysisGroup? group = inspectionRecord.AnalysisGroup;
        List<string> groupedAnalyses =
            inspectionRecord.AnalysisGroup?.Analyses.Select((g) => g.AnalysisType).ToList() ?? [];

        foreach (var analysis in inspectionRecord.Analyses)
        {
            var shouldDefer = group is not null && groupedAnalyses.Contains(analysis.AnalysisType);

            if (shouldDefer)
            {
                logger.LogInformation(
                    "Deferring analysis '{AnalysisName}' for InspectionId: {InspectionId} — waiting for group {GroupId}",
                    Sanitize.SanitizeUserInput(analysis.AnalysisType),
                    Sanitize.SanitizeUserInput(inspectionRecord.InspectionId),
                    Sanitize.SanitizeUserInput(group!.Id.ToString())
                );
            }
            else
            {
                await TriggerAnalysis(analysis, [inspectionRecord]);
            }
        }

        if (group is not null)
        {
            await CheckAndCompleteGroup(group, groupedAnalyses);
        }
    }

    private async Task TriggerAnalysis(
        Analysis analysis,
        IReadOnlyList<InspectionRecord> inspectionRecords
    )
    {
        var analysisConfig = _options.Analyses[analysis.AnalysisType];
        var workflowChain = analysisConfig.Workflows;

        if (workflowChain.Count == 0)
        {
            logger.LogWarning(
                "Analysis '{AnalysisName}' has an empty workflow chain",
                Sanitize.SanitizeUserInput(analysis.AnalysisType)
            );
            return;
        }

        if (inspectionRecords.Count == 0)
        {
            logger.LogWarning(
                "TriggerAnalysis called for analysis '{AnalysisName}' with no InspectionRecords — skipping",
                Sanitize.SanitizeUserInput(analysis.AnalysisType)
            );
            return;
        }

        var run = await CreateAnalysisRun(analysis, inspectionRecords);

        var firstWorkflow = run.Workflows.OrderBy(w => w.StepNumber).First();
        await workflowService.TriggerWorkflow(firstWorkflow);
    }

    private async Task<AnalysisRun> CreateAnalysisRun(
        Analysis analysis,
        IReadOnlyList<InspectionRecord> inspectionRecords
    )
    {
        context.Entry(analysis).State = EntityState.Unchanged;
        foreach (var inspectionRecord in analysis.InspectionRecords)
            context.Entry(inspectionRecord).State = EntityState.Unchanged;
        if (analysis.AnalysisGroup != null)
            context.Entry(analysis.AnalysisGroup).State = EntityState.Unchanged;

        var analysisConfig = _options.Analyses[analysis.AnalysisType];
        var workflowChain = analysisConfig.Workflows;

        var nextRunNumber =
            await context
                .AnalysisRuns.Where(r => r.AnalysisId == analysis.Id)
                .Select(r => (int?)r.RunNumber)
                .MaxAsync()
            ?? 0;
        nextRunNumber += 1;

        var run = new AnalysisRun
        {
            Analysis = analysis,
            RunNumber = nextRunNumber,
            Status = AnalysisRunStatus.InProgress,
            StartedAt = DateTime.UtcNow,
        };

        context.Entry(run.Analysis).State = EntityState.Modified;
        await context.AnalysisRuns.AddAsync(run);
        await context.SaveChangesAsync();

        var workflows = CreateWorkflows(run, workflowChain, inspectionRecords);

        run.Workflows.AddRange(workflows);
        await context.SaveChangesAsync();

        return run;
    }

    private List<Workflow> CreateWorkflows(
        AnalysisRun run,
        List<string> workflowChain,
        IReadOnlyList<InspectionRecord> inspectionRecords
    )
    {
        List<Workflow> worklows = [];
        var firstStepInputs = inspectionRecords.Select(r => r.BlobStorageLocation).ToList();

        for (var i = 0; i < workflowChain.Count; i++)
        {
            var workflowType = workflowChain[i];
            var stepNumber = i + 1;

            // Step 1 is seeded with the inspection record blobs. Later steps start
            // with no inputs — they are wired at result time when the previous
            // workflow reports its actual outputBlobStorageLocation.
            var inputs =
                stepNumber == 1
                    ? [.. firstStepInputs.Select(b => b.Clone())]
                    : new List<BlobStorageLocation>();

            worklows.Add(
                new Workflow
                {
                    AnalysisRun = run,
                    StepNumber = stepNumber,
                    WorkflowType = workflowType,
                    InputBlobStorageLocations = inputs,
                }
            );
        }
        return worklows;
    }

    private async Task CheckAndCompleteGroup(AnalysisGroup group, List<string> groupedAnalyses)
    {
        var recordCount = await context.InspectionRecords.CountAsync(ir =>
            ir.AnalysisGroupId == group.Id
        );

        if (recordCount < group.ExpectedSize)
        {
            logger.LogInformation(
                "Group {GroupId}: {RecordCount}/{ExpectedSize} records received",
                Sanitize.SanitizeUserInput(group.Id.ToString()),
                recordCount,
                group.ExpectedSize
            );
            return;
        }

        group.Status = AnalysisGroupStatus.Complete;
        context.Entry(group).State = EntityState.Modified;
        await context.SaveChangesAsync();

        logger.LogInformation(
            "Group {GroupId} is complete. Triggering grouped analyses: {Analyses}",
            Sanitize.SanitizeUserInput(group.Id.ToString()),
            Sanitize.SanitizeUserInput(string.Join(", ", groupedAnalyses))
        );

        // Resolve all records in the group up-front so we can pass them to TriggerAnalysis
        // and backfill the M:N link on each deferred analysis.
        var groupRecords = await context
            .InspectionRecords.Where(ir => ir.AnalysisGroupId == group.Id)
            .ToListAsync();

        var deferredAnalyses = await context
            .Analyses.Include(a => a.InspectionRecords)
            .Where(a => a.AnalysisGroupId == group.Id && groupedAnalyses.Contains(a.AnalysisType))
            .Where(a => a.Runs.Count == 0)
            .ToListAsync();

        foreach (var analysis in deferredAnalyses)
        {
            // Backfill the M:N association so future lookups (e.g. result handlers) see
            // every record in the group, not just the one that triggered analysis creation.
            foreach (var rec in groupRecords)
            {
                if (!analysis.InspectionRecords.Any(r => r.Id == rec.Id))
                {
                    analysis.InspectionRecords.Add(rec);
                }
            }

            await context.SaveChangesAsync();

            await TriggerAnalysis(analysis, groupRecords);
        }
    }

    public async Task RerunAnalysis(Guid analysisId)
    {
        var analysis = await context
            .Analyses.Include(a => a.InspectionRecords)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == analysisId);

        if (analysis is null)
        {
            throw new KeyNotFoundException($"Analysis with id {analysisId} not found");
        }

        if (!_options.Analyses.ContainsKey(analysis.AnalysisType))
        {
            throw new InvalidOperationException(
                $"Analysis '{analysis.AnalysisType}' is not present in current configuration"
            );
        }

        if (analysis.InspectionRecords.Count == 0)
        {
            throw new InvalidOperationException(
                $"Analysis {analysisId} has no InspectionRecords to rerun against"
            );
        }

        await TriggerAnalysis(analysis, analysis.InspectionRecords);
    }
}
