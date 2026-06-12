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
    IWorkflowService workflowService,
    ILogger<AnalysisTriggerService> logger
) : IAnalysisTriggerService
{
    private readonly AnalysisOptions _options = analysisOptions.Value;

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

    /// <summary>
    /// Returns the configured analyses to run for an InspectionRecord.
    /// Analyses come from the event's explicit <c>RequiredAnalysis</c> when
    /// set; otherwise from <c>DefaultAnalysisByFileExtension</c>. Names that
    /// are not present in configuration are dropped with an error log so
    /// known analyses can still proceed independently. Returns an empty list
    /// when no analyses apply.
    /// </summary>
    private List<string> GetAnalysesToRun(
        InspectionRecordCreatedEvent createdEvent,
        InspectionRecord inspectionRecord
    )
    {
        List<string> requestedNames;
        if (createdEvent.RequiredAnalysis is { Count: > 0 })
        {
            requestedNames = createdEvent.RequiredAnalysis;
        }
        else
        {
            var blobName = inspectionRecord.BlobStorageLocation.BlobName;
            var extension = Path.GetExtension(blobName)?.ToLowerInvariant();
            requestedNames =
                extension is not null
                && _options.DefaultAnalysisByFileExtension.TryGetValue(extension, out var defaults)
                    ? defaults
                    : [];
        }

        if (requestedNames.Count == 0)
        {
            logger.LogInformation(
                "No analyses to run for InspectionId: {InspectionId}",
                Sanitize.SanitizeUserInput(inspectionRecord.InspectionId)
            );
            return [];
        }

        var unknownNames = requestedNames
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

        var knownNames = requestedNames.Where(name => _options.Analyses.ContainsKey(name)).ToList();

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

        // First step takes all input records' blobs; subsequent steps chain on previous output.
        // Clone every BlobStorageLocation assigned to a workflow's inputs so each Workflow owns its
        // own copies — sharing instances across owners (InspectionRecord, sibling Workflows, the
        // current workflow's own output) confuses EF's owned-entity tracking.
        var currentInputs = inspectionRecords.Select(r => r.BlobStorageLocation).ToList();

        for (var i = 0; i < workflowChain.Count; i++)
        {
            var workflowType = workflowChain[i];
            var stepNumber = i + 1;

            var workflow = new Workflow
            {
                AnalysisRun = run,
                StepNumber = stepNumber,
                WorkflowType = workflowType,
                InputBlobStorageLocations = currentInputs.Select(b => b.Clone()).ToList(),
            };
            run.Workflows.Add(workflow);

            var outputLocation = ComputeOutputBlobStorageLocation(
                workflowType,
                stepNumber,
                run.Id,
                currentInputs[0]
            );
            workflow.OutputBlobStorageLocation = outputLocation;

            // Gating steps don't transform the pipeline payload; the next step
            // keeps consuming the previous non-gating step's output.
            if (!_options.Workflows[workflowType].IsGate)
            {
                currentInputs = [outputLocation];
            }
        }

        await context.SaveChangesAsync();

        var firstWorkflow = run.Workflows.OrderBy(w => w.StepNumber).First();
        await workflowService.TriggerWorkflow(firstWorkflow.Id);
    }

    private BlobStorageLocation ComputeOutputBlobStorageLocation(
        string workflowType,
        int stepNumber,
        Guid analysisRunId,
        BlobStorageLocation fallbackInputLocation
    )
    {
        if (!_options.Workflows.TryGetValue(workflowType, out var workflowConfig))
        {
            throw new InvalidOperationException(
                $"Unknown workflow type '{workflowType}' — not found in configuration"
            );
        }

        var extension =
            workflowConfig.OutputFileExtension ?? Path.GetExtension(fallbackInputLocation.BlobName);

        var blobName = $"analysis-runs/{analysisRunId}/{stepNumber}-{workflowType}{extension}";

        var blobContainer = fallbackInputLocation.BlobContainer;

        return new BlobStorageLocation
        {
            StorageAccount = workflowConfig.OutputStorageAccount,
            BlobContainer = blobContainer,
            BlobName = blobName,
        };
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

        // Resolve all records in the group up-front so we can pass them to TriggerAnalysis
        // and backfill the M:N link on each deferred analysis.
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
