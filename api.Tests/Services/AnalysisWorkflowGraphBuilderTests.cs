using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using api.Configurations;
using api.Database.Context;
using api.Database.Models;
using api.Services;
using Api.Test.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Test.Services;

// Pins the Argo payload built from the production definitions for every analysis.
public class AnalysisWorkflowGraphBuilderTests : IAsyncLifetime
{
    private const string AnonymizerAccount = "anonstore";
    private const string VisualizedAccount = "visstore";
    private const string RawAccount = "rawacct";
    private const string RawContainer = "rawcontainer";
    private const string RawBlobName = "raw.jpg";
    private const string Tag = "T1";
    private const string Description = "desc";
    private const string MissionName = "mission-1";
    private const string Installation = "HUA";
    private static readonly DateTime StartedAt = new(2026, 10, 27, 10, 0, 0, DateTimeKind.Utc);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private PostgreSqlContainer _container = null!;
    private TestWebApplicationFactory<Program> _factory = null!;
    private SaraDbContext _context = null!;
    private DatabaseUtilities _db = null!;

    public async ValueTask InitializeAsync()
    {
        (_container, string cs) = await TestSetupHelpers.ConfigurePostgreSqlDatabase();
        _factory = TestSetupHelpers.ConfigureWebApplicationFactory(cs);
        _ = _factory.Services;
        _context = TestSetupHelpers.ConfigurePostgreSqlContext(cs);
        _db = new DatabaseUtilities(_context);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Anonymize_BuildsSingleAnonymizerTask()
    {
        var (run, resource) = await BuildAsync("Anonymize");

        AssertAnonymizerTask(Assert.Single(Tasks(resource)), run);
        AssertPersistedInputs(run.Workflows[0], RawLocation());
    }

    [Fact]
    public async Task ThermalReading_FeedsAnonymizerTiffToThermalReading()
    {
        var metadata = await _db.NewReferencePolygonMetadata(Installation, Tag, Description);

        var (run, resource) = await BuildAsync("ThermalReading");

        var tasks = Tasks(resource);
        Assert.Equal(2, tasks.Count);
        var anonymizer = run.Workflows[0];
        var thermal = run.Workflows[1];
        AssertAnonymizerTask(tasks[0], run);
        AssertTask(
            tasks[1],
            thermal,
            "thermal-reading-workflow-templates",
            depends: DependsOn(anonymizer),
            when: null,
            Json(new[] { Tiff() }),
            Json(Visualized(run, "thermal-reading", ".jpg")),
            Extras(
                ("referenceImageBlobStorageLocation", metadata.ReferenceImageBlobStorageLocation),
                ("referencePolygon", metadata.Polygon)
            )
        );
        AssertPersistedInputs(anonymizer, RawLocation());
        AssertPersistedInputs(thermal, Tiff());
    }

    [Fact]
    public async Task Fencilla_WithReferenceMetadata_PassesExtrasAndInspectionMetadata()
    {
        var metadata = await _db.NewReferencePolygonMetadata(Installation, Tag, Description);

        var (run, resource) = await BuildAsync("Fencilla");

        var tasks = Tasks(resource);
        Assert.Equal(3, tasks.Count);
        var (anonymizer, rain, fencilla) = (run.Workflows[0], run.Workflows[1], run.Workflows[2]);
        AssertAnonymizerTask(tasks[0], run);
        AssertTask(
            tasks[1],
            rain,
            "rain-drop-detector-workflow-templates",
            depends: DependsOn(anonymizer),
            when: null,
            Json(new[] { AnonymizerOutputLocation(run) }),
            Json(Visualized(run, "rain-drop", ".json")),
            Extras()
        );
        AssertTask(
            tasks[2],
            fencilla,
            "fencilla-workflow-templates",
            depends: DependsOn(rain),
            when: RainGate(rain),
            Json(new[] { AnonymizerOutputLocation(run) }),
            Json(Visualized(run, "fencilla", ".jpg")),
            Extras(
                ("referenceImageBlobStorageLocation", metadata.ReferenceImageBlobStorageLocation),
                ("referencePolygon", metadata.Polygon)
            ),
            (
                "inspectionMetadata",
                Json(new[] { new { MissionName, InspectionDescription = Description } })
            )
        );
        AssertPersistedInputs(rain, AnonymizerOutputLocation(run));
        AssertPersistedInputs(fencilla, AnonymizerOutputLocation(run));
    }

    [Fact]
    public async Task Fencilla_WithoutReferenceMetadata_SendsEmptyExtras()
    {
        var (run, resource) = await BuildAsync("Fencilla");

        var fencilla = Tasks(resource)[2];
        AssertTask(
            fencilla,
            run.Workflows[2],
            "fencilla-workflow-templates",
            depends: DependsOn(run.Workflows[1]),
            when: RainGate(run.Workflows[1]),
            Json(new[] { AnonymizerOutputLocation(run) }),
            Json(Visualized(run, "fencilla", ".jpg")),
            Extras(),
            (
                "inspectionMetadata",
                Json(new[] { new { MissionName, InspectionDescription = Description } })
            )
        );
    }

    [Fact]
    public async Task ThermalReading_WithoutReferenceMetadata_Throws() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildAsync("ThermalReading"));

    [Fact]
    public async Task Cloe_ReadsAnonymizerOutputAcrossRainGate()
    {
        var (run, resource) = await BuildAsync("CLOE");

        var tasks = Tasks(resource);
        Assert.Equal(3, tasks.Count);
        var (anonymizer, rain, cloe) = (run.Workflows[0], run.Workflows[1], run.Workflows[2]);
        AssertAnonymizerTask(tasks[0], run);
        AssertTask(
            tasks[1],
            rain,
            "rain-drop-detector-workflow-templates",
            depends: DependsOn(anonymizer),
            when: null,
            Json(new[] { AnonymizerOutputLocation(run) }),
            Json(Visualized(run, "rain-drop", ".json")),
            Extras()
        );
        AssertTask(
            tasks[2],
            cloe,
            "cloe-workflow-templates",
            depends: DependsOn(rain),
            when: RainGate(rain),
            Json(new[] { AnonymizerOutputLocation(run) }),
            Json(Visualized(run, "cloe", ".jpg")),
            Extras()
        );
        AssertPersistedInputs(cloe, AnonymizerOutputLocation(run));
    }

    [Fact]
    public async Task Passthrough_CopiesRawWithOperationExtra()
    {
        var (run, resource) = await BuildAsync("Passthrough");

        var task = Assert.Single(Tasks(resource));
        AssertTask(
            task,
            run.Workflows[0],
            "utilities-workflow-templates",
            depends: null,
            when: null,
            Raw(),
            Json(Visualized(run, "copy-raw-to-visualized", ".jpg")),
            Extras(("operation", "copy-raw-to-visualized"))
        );
        AssertPersistedInputs(run.Workflows[0], RawLocation());
    }

    private async Task<(AnalysisRun Run, ArgoWorkflowResource Resource)> BuildAsync(
        string analysisType
    )
    {
        var options = LoadProductionOptions();
        var definitions = _factory.Services.GetRequiredService<AnalysisAndWorkflowDefinitions>();
        var chain = definitions.Analyses[analysisType].Steps.Select(step => step.Workflow).ToList();

        var record = await _db.NewInspectionRecord(
            installationCode: Installation,
            inspectionId: "insp-1",
            blobStorageAccount: RawAccount,
            blobContainer: RawContainer,
            blobName: RawBlobName,
            tag: Tag,
            inspectionDescription: Description,
            missionName: MissionName
        );
        var analysis = await _db.NewAnalysis(analysisType, [record]);
        var run = await _db.NewAnalysisRun(analysis);
        run.StartedAt = StartedAt;
        for (var i = 0; i < chain.Count; i++)
        {
            await _db.NewWorkflow(
                run,
                chain[i],
                stepNumber: i + 1,
                inputBlobStorageLocations: i == 0 ? [RawLocation()] : []
            );
        }
        run.Workflows = [.. run.Workflows.OrderBy(w => w.StepNumber)];

        using var scope = _factory.Services.CreateScope();
        var referenceService =
            scope.ServiceProvider.GetRequiredService<IReferencePolygonMetadataService>();
        var builder = new AnalysisWorkflowGraphBuilder(
            _context,
            definitions,
            new AnalysisPlanner(definitions, options, referenceService)
        );

        var resource = await builder.BuildArgoWorkflowAsync(run);
        return (run, resource);
    }

    private static IOptions<AnalysisOptions> LoadProductionOptions()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "DatabaseSettings", "appsettings.json");
        var analysisOptions = new ConfigurationBuilder()
            .AddJsonFile(path)
            .Build()
            .GetSection(AnalysisOptions.SectionName)
            .Get<AnalysisOptions>()!;
        foreach (var (name, workflow) in analysisOptions.Workflows)
        {
            workflow.OutputStorageAccount =
                name == "anonymizer" ? AnonymizerAccount : VisualizedAccount;
        }
        return Options.Create(analysisOptions);
    }

    private static List<ArgoDagTask> Tasks(ArgoWorkflowResource resource) =>
        resource.Spec!.Templates[0].Dag.Tasks;

    private static BlobStorageLocation RawLocation() =>
        new()
        {
            StorageAccount = RawAccount,
            BlobContainer = RawContainer,
            BlobName = RawBlobName,
        };

    private static string Raw() => Json(new[] { RawLocation() });

    private static BlobStorageLocation Tiff() =>
        new()
        {
            StorageAccount = AnonymizerAccount,
            BlobContainer = RawContainer,
            BlobName = "raw.tiff",
        };

    private static string OutputName(AnalysisRun run, string workflowType, string extension) =>
        $"2026-10-27/10-00-00/tag__{Tag}__workflowtype__{workflowType}__analysisrunid__{run.Id}{extension}";

    private static BlobStorageLocation AnonymizerOutputLocation(AnalysisRun run) =>
        new()
        {
            StorageAccount = AnonymizerAccount,
            BlobContainer = RawContainer,
            BlobName = OutputName(run, "anonymizer", ".jpg"),
        };

    private static string AnonymizerOutput(AnalysisRun run) => Json(AnonymizerOutputLocation(run));

    private static BlobStorageLocation Visualized(
        AnalysisRun run,
        string workflowType,
        string extension
    ) =>
        new()
        {
            StorageAccount = VisualizedAccount,
            BlobContainer = RawContainer,
            BlobName = OutputName(run, workflowType, extension),
        };

    private static string Extras(params (string Key, object Value)[] entries) =>
        Json(entries.ToDictionary(entry => entry.Key, entry => entry.Value));

    private static string Json(object value) => JsonSerializer.Serialize(value, JsonOptions);

    private static string DependsOn(Workflow previous)
    {
        var name = AnalysisWorkflowGraphBuilder.GetTaskName(previous);
        return $"{name}.Succeeded || {name}.Skipped || {name}.Omitted";
    }

    private static string RainGate(Workflow rainDrop) =>
        "{{=jsonpath(tasks['"
        + AnalysisWorkflowGraphBuilder.GetTaskName(rainDrop)
        + "'].outputs.parameters.result, '$.rain') != true}}";

    private static void AssertAnonymizerTask(ArgoDagTask task, AnalysisRun run) =>
        AssertTask(
            task,
            run.Workflows[0],
            "anonymizer-workflow-templates",
            depends: null,
            when: null,
            Raw(),
            AnonymizerOutput(run),
            Extras(("preProcessedBlobStorageLocation", Tiff()))
        );

    private static void AssertTask(
        ArgoDagTask task,
        Workflow workflow,
        string templateName,
        string? depends,
        string? when,
        string input,
        string output,
        string extras,
        params (string Name, string Value)[] additionalParameters
    )
    {
        Assert.Equal(AnalysisWorkflowGraphBuilder.GetTaskName(workflow), task.Name);
        Assert.Equal(templateName, task.TemplateRef.Name);
        Assert.Equal("main", task.TemplateRef.Template);
        Assert.Equal(depends, task.Depends);
        Assert.Equal(when, task.When);

        List<(string, string?)> expected =
        [
            ("inputBlobStorageLocations", input),
            ("outputBlobStorageLocation", output),
            ("extras", extras),
            .. additionalParameters.Select(p => (p.Name, (string?)p.Value)),
        ];
        Assert.Equal(expected, task.Arguments.Parameters.Select(p => (p.Name, p.Value)).ToList());
    }

    private static void AssertPersistedInputs(
        Workflow workflow,
        params BlobStorageLocation[] expected
    ) =>
        Assert.Equal(
            expected.Select(location => location.ToString()),
            workflow.InputBlobStorageLocations.Select(location => location.ToString())
        );
}
