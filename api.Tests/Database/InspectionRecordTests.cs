using System;
using System.Collections.Generic;
using api.Database.Models;
using Xunit;

namespace Api.Test.Database;

public class InspectionRecordTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FindLatestWorkflow_ReturnsFinalStepOfMatchingAnalysis()
    {
        var record = new InspectionRecord
        {
            InspectionId = "inspection",
            InstallationCode = "TST",
            BlobStorageLocation = Blob("raw"),
        };
        var run = AddRun(record, AnalysisTypeEnum.CLOE);
        AddStep(run, 1, "anonymizer", Blob("raw"), Now.AddMinutes(-2));
        AddStep(run, 2, "rain-drop", Blob("anonymized"), Now.AddMinutes(-1));
        var final = AddStep(run, 3, "cloe", Blob("preprocessed"), Now);

        var workflow = record.FindLatestWorkflow(AnalysisTypeEnum.CLOE);

        Assert.Same(final, workflow);
        Assert.Equal("preprocessed", workflow!.InputBlobStorageLocations[0].BlobName);
    }

    [Fact]
    public void FindLatestWorkflow_IgnoresOtherAnalysisTypes()
    {
        var record = new InspectionRecord
        {
            InspectionId = "inspection",
            InstallationCode = "TST",
            BlobStorageLocation = Blob("raw"),
        };
        var fencillaRun = AddRun(record, AnalysisTypeEnum.Fencilla);
        AddStep(fencillaRun, 1, "anonymizer", Blob("raw"), Now);

        Assert.Null(record.FindLatestWorkflow(AnalysisTypeEnum.ThermalReading));
        Assert.NotNull(record.FindLatestWorkflow(AnalysisTypeEnum.Fencilla));
    }

    [Fact]
    public void FindLatestWorkflow_PrefersCompletedRunOverPendingRerun()
    {
        var record = new InspectionRecord
        {
            InspectionId = "inspection",
            InstallationCode = "TST",
            BlobStorageLocation = Blob("raw"),
        };
        var firstRun = AddRun(record, AnalysisTypeEnum.ThermalReading, runNumber: 1);
        var firstFinal = AddStep(firstRun, 2, "thermal-reading", Blob("first"), Now);

        var rerun = AddRun(record, AnalysisTypeEnum.ThermalReading, runNumber: 2);
        AddStep(rerun, 1, "anonymizer", Blob("raw"), null);
        AddStep(rerun, 2, "thermal-reading", inputBlob: null, completedAt: null);

        var workflow = record.FindLatestWorkflow(AnalysisTypeEnum.ThermalReading);

        Assert.Same(firstFinal, workflow);
    }

    [Fact]
    public void FindLatestWorkflow_PrefersNewerCompletedRun()
    {
        var record = new InspectionRecord
        {
            InspectionId = "inspection",
            InstallationCode = "TST",
            BlobStorageLocation = Blob("raw"),
        };
        var firstRun = AddRun(record, AnalysisTypeEnum.ThermalReading, runNumber: 1);
        AddStep(firstRun, 2, "thermal-reading", Blob("first"), Now.AddHours(-1));

        var rerun = AddRun(record, AnalysisTypeEnum.ThermalReading, runNumber: 2);
        var rerunFinal = AddStep(rerun, 2, "thermal-reading", Blob("second"), Now);

        var workflow = record.FindLatestWorkflow(AnalysisTypeEnum.ThermalReading);

        Assert.Same(rerunFinal, workflow);
    }

    private static AnalysisRun AddRun(
        InspectionRecord record,
        AnalysisTypeEnum analysisType,
        int runNumber = 1
    )
    {
        var analysis = new Analysis { AnalysisType = analysisType.ToString() };
        var run = new AnalysisRun { Analysis = analysis, RunNumber = runNumber };
        analysis.Runs.Add(run);
        record.Analyses.Add(analysis);
        return run;
    }

    private static Workflow AddStep(
        AnalysisRun run,
        int stepNumber,
        string workflowType,
        BlobStorageLocation? inputBlob,
        DateTime? completedAt
    )
    {
        var workflow = new Workflow
        {
            AnalysisRun = run,
            StepNumber = stepNumber,
            WorkflowType = workflowType,
            InputBlobStorageLocations = inputBlob is null ? [] : [inputBlob],
            CompletedAt = completedAt,
            Status = completedAt is null ? WorkflowStatus.Pending : WorkflowStatus.Succeeded,
        };
        run.Workflows.Add(workflow);
        return workflow;
    }

    private static BlobStorageLocation Blob(string name) =>
        new()
        {
            StorageAccount = "teststorage",
            BlobContainer = "test-container",
            BlobName = name,
        };
}
