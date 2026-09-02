using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api.Database.Context;
using api.Database.Models;
using api.MQTT;
using Microsoft.EntityFrameworkCore;

namespace Api.Test.Database;

/// <summary>
/// Seed helpers for tests. Creates and persists the minimum entities needed
/// to exercise SARA's services.
/// </summary>
public class DatabaseUtilities(SaraDbContext context)
{
    private readonly SaraDbContext _context = context;

    public async Task<InspectionRecord> NewInspectionRecord(
        string installationCode = "TST",
        string inspectionId = "test-inspection",
        string blobStorageAccount = "teststorage",
        string blobContainer = "test-container",
        string? blobName = null,
        string? inspectionType = null,
        string? tag = null,
        string? inspectionDescription = null,
        string? missionName = null,
        List<Analysis>? analyses = null,
        AnalysisGroup? analysisGroup = null
    )
    {
        foreach (var analysis in analyses ?? [])
        {
            _context.Entry(analysis).State = EntityState.Unchanged;
            if (analysis.AnalysisGroup != null)
                _context.Entry(analysis.AnalysisGroup).State = EntityState.Unchanged;
        }

        var record = new InspectionRecord
        {
            InspectionId = inspectionId,
            InstallationCode = installationCode,
            BlobStorageLocation = NewBlobStorageLocation(
                blobStorageAccount,
                blobContainer,
                blobName
            ),
            InspectionType = inspectionType,
            Tag = tag,
            InspectionDescription = inspectionDescription,
            MissionName = missionName,
            Analyses = analyses ?? [],
            AnalysisGroup = analysisGroup,
        };

        _context.InspectionRecords.Add(record);
        await _context.SaveChangesAsync();

        foreach (var analysis in record.Analyses)
        {
            if (analysis.AnalysisGroup != null)
                _context.Entry(analysis.AnalysisGroup).State = EntityState.Detached;
            _context.Entry(analysis).State = EntityState.Detached;
        }
        _context.Entry(record).State = EntityState.Detached;
        return record;
    }

    public async Task<ThermalReferenceMetadata> NewThermalReferenceMetadata(
        string installationCode = "TST",
        string tagId = "test-tag",
        string inspectionDescription = "test-description"
    )
    {
        var metadata = new ThermalReferenceMetadata
        {
            InstallationCode = installationCode,
            TagId = tagId,
            InspectionDescription = inspectionDescription,
            ReferenceImageBlobStorageLocation = NewBlobStorageLocation(
                blobContainer: "thermal-reference",
                blobName: $"{Guid.NewGuid()}.jpg"
            ),
            ReferencePolygonBlobStorageLocation = NewBlobStorageLocation(
                blobContainer: "thermal-reference",
                blobName: $"{Guid.NewGuid()}.json"
            ),
        };

        _context.ThermalReferenceMetadata.Add(metadata);
        await _context.SaveChangesAsync();
        return metadata;
    }

    public async Task<Analysis> NewAnalysis(
        string type = "test-analysis",
        IEnumerable<InspectionRecord>? inspectionRecords = null,
        AnalysisGroup? analysisGroup = null
    )
    {
        foreach (var inspectionRecord in inspectionRecords ?? [])
            _context.Entry(inspectionRecord).State = EntityState.Unchanged;

        var analysis = new Analysis { AnalysisType = type };
        if (inspectionRecords is not null)
        {
            analysis.InspectionRecords.AddRange(inspectionRecords);
        }
        if (analysisGroup is not null)
        {
            analysis.AnalysisGroupId = analysisGroup.Id;
        }
        _context.Analyses.Add(analysis);
        await _context.SaveChangesAsync();
        return analysis;
    }

    public async Task<AnalysisGroup> NewAnalysisGroup(
        string groupId = "test-group",
        int expectedSize = 2,
        AnalysisGroupStatus status = AnalysisGroupStatus.Pending,
        DateTime? timeoutAt = null
    )
    {
        var group = new AnalysisGroup
        {
            GroupId = groupId,
            ExpectedSize = expectedSize,
            Status = status,
            TimeoutAt = timeoutAt,
        };
        _context.AnalysisGroups.Add(group);
        await _context.SaveChangesAsync();
        return group;
    }

    public async Task<AnalysisRun> NewAnalysisRun(Analysis analysis, int runNumber = 1)
    {
        var run = new AnalysisRun
        {
            Analysis = analysis,
            AnalysisId = analysis.Id,
            RunNumber = runNumber,
        };
        _context.AnalysisRuns.Add(run);
        await _context.SaveChangesAsync();
        return run;
    }

    public async Task<Workflow> NewWorkflow(
        AnalysisRun run,
        string workflowType = "test-workflow",
        int stepNumber = 1,
        IEnumerable<BlobStorageLocation>? inputBlobStorageLocations = null,
        BlobStorageLocation? outputBlobStorageLocation = null
    )
    {
        var workflow = new Workflow
        {
            AnalysisRun = run,
            AnalysisRunId = run.Id,
            StepNumber = stepNumber,
            WorkflowType = workflowType,
            InputBlobStorageLocations = inputBlobStorageLocations is null
                ? [NewBlobStorageLocation()]
                : [.. inputBlobStorageLocations],
            OutputBlobStorageLocation = outputBlobStorageLocation,
        };
        _context.Workflows.Add(workflow);
        await _context.SaveChangesAsync();
        return workflow;
    }

    public BlobStorageLocation NewBlobStorageLocation(
        string storageAccount = "teststorage",
        string blobContainer = "test-container",
        string? blobName = null
    ) =>
        new()
        {
            StorageAccount = storageAccount,
            BlobContainer = blobContainer,
            BlobName = blobName ?? $"{Guid.NewGuid()}.jpg",
        };

    public IsarAnalysisGroupMessage NewAnalysisGroupMessage(
        string groupId = "test-group",
        int size = 2,
        List<string>? analyses = null
    ) =>
        new()
        {
            AnalysisGroupId = groupId,
            AnalysisGroupSize = size,
            AnalysisGroupAnalyses = analyses ?? ["group-test"],
        };

    public IsarInspectionResultMessage NewIsarInspectionResultMessage(
        string inspectionId = "test-inspection",
        string installationCode = "TST",
        string tagId = "test-tag",
        string inspectionType = "Image",
        string inspectionDescription = "test-description",
        string robotName = "test-robot",
        string isarId = "test-isar",
        string missionName = "test-mission",
        string fileType = "jpg",
        string blobStorageAccount = "teststorage",
        string blobContainer = "test-container",
        string? blobName = null,
        List<string>? requiredAnalysis = null,
        IsarAnalysisGroupMessage? analysisGroup = null,
        Pose? robotPose = null,
        Position? targetPosition = null
    )
    {
        var dataPath = new InspectionPathMessage
        {
            StorageAccount = blobStorageAccount,
            BlobContainer = blobContainer,
            BlobName = blobName ?? $"{Guid.NewGuid()}.jpg",
        };
        return new IsarInspectionResultMessage
        {
            IsarId = isarId,
            RobotName = robotName,
            InspectionId = inspectionId,
            MissionName = missionName,
            InspectionDataPath = dataPath,
            InstallationCode = installationCode,
            TagId = tagId,
            InspectionType = inspectionType,
            InspectionDescription = inspectionDescription,
            Timestamp = DateTime.UtcNow,
            RequiredAnalysis = requiredAnalysis,
            AnalysisGroup = analysisGroup,
            RobotPose = robotPose,
            TargetPosition = targetPosition,
            FileType = fileType,
        };
    }
}
