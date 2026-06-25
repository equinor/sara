using api.Configurations;
using api.Database.Context;
using api.Database.Models;
using api.MQTT;
using api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace api.Services;

public interface IAnalysisTriggerService
{
    public Task OnInspectionRecordCreated(InspectionRecordCreatedEvent createdEvent);

    public Task RerunAnalysis(Guid analysisId);
}

public class AnalysisTriggerService(
    SaraDbContext context,
    IOptions<AnalysisOptions> analysisOptions,
    IEnumerable<ITriggerPayloadEnricher> payloadEnrichers,
    IWorkflowGraphBuilder workflowGraphBuilder,
    IArgoWorkflowSubmitter argoWorkflowSubmitter,
    ILogger<AnalysisTriggerService> logger
) : IAnalysisTriggerService
{
    private readonly AnalysisOptions _options = analysisOptions.Value;

    private readonly Dictionary<string, ITriggerPayloadEnricher> _enrichersByType =
        payloadEnrichers.ToDictionary(e => e.WorkflowType, StringComparer.OrdinalIgnoreCase);

    public async Task OnInspectionRecordCreated(InspectionRecordCreatedEvent createdEvent)
    {
        var inspectionRecord = await context.InspectionRecords.FirstOrDefaultAsync(r =>
            r.Id == createdEvent.InspectionRecordId
        );
        if (inspectionRecord is null)
        {
            logger.LogError(
                "InspectionRecord {InspectionRecordId} not found when handling created event",
                createdEvent.InspectionRecordId
            );
            return;
        }

        var analysisNames = GetAnalysesToRun(createdEvent, inspectionRecord);
        if (analysisNames.Count == 0)
        {
            return;
        }

        AnalysisGroup? group = null;
        List<string> groupedAnalyses = [];

        if (createdEvent.AnalysisGroup is not null)
        {
            group = await GetOrCreateAnalysisGroup(createdEvent.AnalysisGroup);
            groupedAnalyses = createdEvent.AnalysisGroup.AnalysisGroupAnalyses;

            inspectionRecord.AnalysisGroupId = group.Id;
            await context.SaveChangesAsync();
        }

        foreach (var analysisName in analysisNames)
        {
            var shouldDefer = group is not null && groupedAnalyses.Contains(analysisName);
            var analysis = await GetOrCreateAnalysis(
                analysisName,
                inspectionRecord,
                group,
                shouldDefer
            );

            if (shouldDefer)
            {
                logger.LogInformation(
                    "Deferring analysis '{AnalysisName}' for InspectionId: {InspectionId} — waiting for group {GroupId}",
                    Sanitize.SanitizeUserInput(analysisName),
                    Sanitize.SanitizeUserInput(inspectionRecord.InspectionId),
                    Sanitize.SanitizeUserInput(group!.GroupId)
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

    private List<string> GetAnalysesToRun(
        InspectionRecordCreatedEvent createdEvent,
        InspectionRecord inspectionRecord
    )
    {
        List<string> analysesToRun;
        if (createdEvent.RequiredAnalysis is { Count: > 0 })
        {
            analysesToRun = createdEvent.RequiredAnalysis;
        }
        else
        {
            var blobName = inspectionRecord.BlobStorageLocation.BlobName;
            var extension = Path.GetExtension(blobName)?.ToLowerInvariant();
            var inspectionType = inspectionRecord.InspectionType;

            analysesToRun = ResolveDefaultAnalyses(inspectionType, extension);
        }

        if (analysesToRun.Count == 0)
        {
            logger.LogInformation(
                "No analyses to run for InspectionId: {InspectionId}",
                Sanitize.SanitizeUserInput(inspectionRecord.InspectionId)
            );
            return [];
        }

        var unknownNames = analysesToRun
            .Where(name => !_options.Analyses.ContainsKey(name))
            .ToList();
        if (unknownNames.Count > 0)
        {
            logger.LogError(
                "Unknown analyses [{UnknownAnalyses}] for InspectionId: {InspectionId} — "
                    + "not found in configuration. These will be skipped.",
                Sanitize.SanitizeUserInput(string.Join(", ", unknownNames)),
                Sanitize.SanitizeUserInput(inspectionRecord.InspectionId)
            );
        }

        var knownNames = analysesToRun.Where(name => _options.Analyses.ContainsKey(name)).ToList();

        if (knownNames.Count == 0)
        {
            logger.LogInformation(
                "No known analyses to run for InspectionId: {InspectionId}",
                Sanitize.SanitizeUserInput(inspectionRecord.InspectionId)
            );
            return [];
        }

        logger.LogInformation(
            "Resolved analyses for InspectionId: {InspectionId}: {Analyses}",
            Sanitize.SanitizeUserInput(inspectionRecord.InspectionId),
            Sanitize.SanitizeUserInput(string.Join(", ", knownNames))
        );

        return knownNames;
    }

    private async Task<AnalysisGroup> GetOrCreateAnalysisGroup(
        IsarAnalysisGroupMessage groupMessage
    )
    {
        var existing = await context.AnalysisGroups.FirstOrDefaultAsync(g =>
            g.GroupId == groupMessage.AnalysisGroupId
        );

        if (existing is not null)
        {
            return existing;
        }

        var timeoutMinutes = _options.AnalysisGroupTimeoutMinutes;
        var group = new AnalysisGroup
        {
            GroupId = groupMessage.AnalysisGroupId,
            ExpectedSize = groupMessage.AnalysisGroupSize,
            TimeoutAt = DateTime.UtcNow.AddMinutes(timeoutMinutes),
        };

        await context.AnalysisGroups.AddAsync(group);
        await context.SaveChangesAsync();

        logger.LogInformation(
            "Created analysis group {GroupId} expecting {ExpectedSize} records, timeout at {TimeoutAt}",
            Sanitize.SanitizeUserInput(group.GroupId),
            group.ExpectedSize,
            group.TimeoutAt
        );

        return group;
    }

    private async Task<Analysis> GetOrCreateAnalysis(
        string analysisName,
        InspectionRecord inspectionRecord,
        AnalysisGroup? group,
        bool shouldDefer
    )
    {
        if (shouldDefer && group is not null)
        {
            var existing = await context
                .Analyses.Include(a => a.InspectionRecords)
                .FirstOrDefaultAsync(a => a.AnalysisGroupId == group.Id && a.Name == analysisName);

            if (existing is not null)
            {
                if (!existing.InspectionRecords.Any(r => r.Id == inspectionRecord.Id))
                {
                    existing.InspectionRecords.Add(inspectionRecord);
                    await context.SaveChangesAsync();
                }
                return existing;
            }
        }

        var analysis = new Analysis { Name = analysisName, AnalysisGroupId = group?.Id };

        analysis.InspectionRecords.Add(inspectionRecord);
        await context.Analyses.AddAsync(analysis);
        await context.SaveChangesAsync();

        return analysis;
    }

    /// <summary>
    /// Persist the full <see cref="AnalysisRun"/> with one
    /// <see cref="Workflow"/> per chain step, compute the per-step
    /// blob-storage I/O graph via <see cref="IWorkflowGraphBuilder"/>, and
    /// submit a single Argo <c>Workflow</c> CR that orchestrates the entire
    /// chain. On failure the run and all its workflows are marked Failed.
    /// </summary>
    private async Task TriggerAnalysis(
        Analysis analysis,
        IReadOnlyList<InspectionRecord> inspectionRecords
    )
    {
        var analysisConfig = _options.Analyses[analysis.Name];
        var workflowChain = analysisConfig.Workflows;

        if (workflowChain.Count == 0)
        {
            logger.LogWarning(
                "Analysis '{AnalysisName}' has an empty workflow chain",
                Sanitize.SanitizeUserInput(analysis.Name)
            );
            return;
        }

        if (inspectionRecords.Count == 0)
        {
            logger.LogWarning(
                "TriggerAnalysis called for analysis '{AnalysisName}' with no InspectionRecords — skipping",
                Sanitize.SanitizeUserInput(analysis.Name)
            );
            return;
        }

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
        await context.AnalysisRuns.AddAsync(run);
        await context.SaveChangesAsync();

        // Create one Workflow row per chain step; the graph builder fills in
        // the input/output BlobStorageLocations below.
        for (var i = 0; i < workflowChain.Count; i++)
        {
            var workflow = new Workflow
            {
                AnalysisRun = run,
                StepNumber = i + 1,
                WorkflowType = workflowChain[i],
                InputBlobStorageLocations = [],
            };
            run.Workflows.Add(workflow);
        }

        var orderedWorkflows = run.Workflows.OrderBy(w => w.StepNumber).ToList();
        var initialInputs = inspectionRecords.Select(r => r.BlobStorageLocation).ToList();
        workflowGraphBuilder.ComputeBlobLocations(run, orderedWorkflows, initialInputs);

        await context.SaveChangesAsync();

        await SubmitAnalysisRunToArgo(run, analysis.Name, orderedWorkflows, inspectionRecords);
    }

    private async Task SubmitAnalysisRunToArgo(
        AnalysisRun run,
        string analysisName,
        IReadOnlyList<Workflow> orderedWorkflows,
        IReadOnlyList<InspectionRecord> inspectionRecords
    )
    {
        try
        {
            var extrasByWorkflowId = new Dictionary<Guid, Dictionary<string, object>>();
            foreach (var step in orderedWorkflows)
            {
                if (_enrichersByType.TryGetValue(step.WorkflowType, out var enricher))
                {
                    extrasByWorkflowId[step.Id] = await enricher.EnrichAsync(
                        step,
                        inspectionRecords
                    );
                }
            }

            var manifest = workflowGraphBuilder.Build(
                run,
                analysisName,
                orderedWorkflows,
                extrasByWorkflowId
            );

            var now = DateTime.UtcNow;
            foreach (var step in orderedWorkflows)
            {
                step.Status = WorkflowStatus.InProgress;
                step.StartedAt ??= now;
            }
            await context.SaveChangesAsync();

            var crName = await argoWorkflowSubmitter.SubmitAsync(manifest);

            logger.LogInformation(
                "Submitted AnalysisRun {AnalysisRunId} ('{AnalysisName}') as Argo Workflow '{CrName}' with {StepCount} step(s)",
                run.Id,
                Sanitize.SanitizeUserInput(analysisName),
                Sanitize.SanitizeUserInput(crName),
                orderedWorkflows.Count
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to submit AnalysisRun {AnalysisRunId} ('{AnalysisName}') to Argo: {ErrorMessage}",
                run.Id,
                Sanitize.SanitizeUserInput(analysisName),
                ex.Message
            );

            var now = DateTime.UtcNow;
            foreach (var step in orderedWorkflows)
            {
                step.Status = WorkflowStatus.Failed;
                step.CompletedAt ??= now;
                step.ErrorMessage ??= ex.Message;
            }
            run.Status = AnalysisRunStatus.Failed;
            run.CompletedAt = now;
            await context.SaveChangesAsync();
        }
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
                Sanitize.SanitizeUserInput(group.GroupId),
                recordCount,
                group.ExpectedSize
            );
            return;
        }

        group.Status = AnalysisGroupStatus.Complete;
        await context.SaveChangesAsync();

        logger.LogInformation(
            "Group {GroupId} is complete. Triggering grouped analyses: {Analyses}",
            Sanitize.SanitizeUserInput(group.GroupId),
            Sanitize.SanitizeUserInput(string.Join(", ", groupedAnalyses))
        );

        var groupRecords = await context
            .InspectionRecords.Where(ir => ir.AnalysisGroupId == group.Id)
            .ToListAsync();

        var deferredAnalyses = await context
            .Analyses.Include(a => a.InspectionRecords)
            .Where(a => a.AnalysisGroupId == group.Id && groupedAnalyses.Contains(a.Name))
            .Where(a => a.Runs.Count == 0)
            .ToListAsync();

        foreach (var analysis in deferredAnalyses)
        {
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

    private List<string> ResolveDefaultAnalyses(string? inspectionType, string? extension)
    {
        return TryGetDefaultAnalysesByInspectionTypeAndExtension(inspectionType, extension)
            ?? TryGetDefaultAnalysesByExtension(extension)
            ?? [];
    }

    private List<string>? TryGetDefaultAnalysesByInspectionTypeAndExtension(
        string? inspectionType,
        string? extension
    )
    {
        if (
            inspectionType is not null
            && extension is not null
            && _options.DefaultAnalysisByInspectionTypeAndExtension.TryGetValue(
                inspectionType,
                out var byExtension
            )
            && byExtension.TryGetValue(extension, out var analyses)
        )
        {
            return analyses;
        }

        return null;
    }

    private List<string>? TryGetDefaultAnalysesByExtension(string? extension)
    {
        if (
            extension is not null
            && _options.DefaultAnalysisByFileExtension.TryGetValue(extension, out var analyses)
        )
        {
            return analyses;
        }

        return null;
    }

    public async Task RerunAnalysis(Guid analysisId)
    {
        var analysis = await context
            .Analyses.Include(a => a.InspectionRecords)
            .FirstOrDefaultAsync(a => a.Id == analysisId);

        if (analysis is null)
        {
            throw new KeyNotFoundException($"Analysis with id {analysisId} not found");
        }

        if (!_options.Analyses.ContainsKey(analysis.Name))
        {
            throw new InvalidOperationException(
                $"Analysis '{analysis.Name}' is not present in current configuration"
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
