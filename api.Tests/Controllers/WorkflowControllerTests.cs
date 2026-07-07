using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using api.Controllers.Models;
using api.Database.Context;
using api.Database.Models;
using api.Services;
using Api.Test.Database;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Test.Controllers;

public class WorkflowControllerTests : IAsyncLifetime
{
    private TestWebApplicationFactory<Program> _factory = null!;
    private SaraDbContext _context = null!;
    private DatabaseUtilities _db = null!;
    public required HttpClient Client;

    public required IWorkflowService WorkflowService;

    public async ValueTask InitializeAsync()
    {
        (var _container, string cs) = await TestSetupHelpers.ConfigurePostgreSqlDatabase();
        _factory = TestSetupHelpers.ConfigureWebApplicationFactory(cs);
        _context = TestSetupHelpers.ConfigurePostgreSqlContext(cs);
        _db = new DatabaseUtilities(_context);

        Client = TestSetupHelpers.ConfigureHttpClient(_factory);

        WorkflowService = _factory.Services.GetRequiredService<IWorkflowService>();
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task CheckThatDTOIsCorrectlyFormattedForFencillaAnalysis()
    {
        // Arrange
        var record = await _db.NewInspectionRecord(
            blobName: "test",
            inspectionType: "fencilla",
            tag: "test-tag",
            inspectionDescription: "test-descr"
        );
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "fencilla");
        workflow.Status = WorkflowStatus.Succeeded;
        workflow.ResultJson = JsonSerializer.Serialize(new { isBreak = false, confidence = 0.5f });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        string Url = $"/api/workflow/id/{workflow.Id}";
        var response = await Client.GetAsync(Url, TestContext.Current.CancellationToken);

        var jsonOptions = new JsonSerializerOptions();
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        jsonOptions.PropertyNameCaseInsensitive = true;
        jsonOptions.IncludeFields = true;

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        var workflowDto = await response.Content.ReadFromJsonAsync<WorkflowDto>(
            jsonOptions,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(workflowDto);
        Assert.Equal(workflowDto.Id, workflow.Id);
        Assert.NotNull(workflowDto.Result);
        Assert.Equal(50f, workflowDto.Result.Confidence);
    }

    [Fact]
    public async Task CheckThatDTOIsCorrectlyFormattedForCLOEAnalysis()
    {
        // Arrange
        const float oilLevel = 0.42f;
        const float confidence = 0.93f;

        var record = await _db.NewInspectionRecord(
            blobName: "test",
            inspectionType: "cloe",
            tag: "test-tag",
            inspectionDescription: "test-descr"
        );
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "cloe");
        workflow.Status = WorkflowStatus.Succeeded;
        workflow.ResultJson = JsonSerializer.Serialize(
            new
            {
                oilLevel = oilLevel,
                confidence = confidence,
                warning = (string?)null,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        string Url = $"/api/workflow/id/{workflow.Id}";
        var response = await Client.GetAsync(Url, TestContext.Current.CancellationToken);

        var jsonOptions = new JsonSerializerOptions();
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        jsonOptions.PropertyNameCaseInsensitive = true;
        jsonOptions.IncludeFields = true;

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        var workflowDto = await response.Content.ReadFromJsonAsync<WorkflowDto>(
            jsonOptions,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(workflowDto);
        Assert.Equal(workflowDto.Id, workflow.Id);
        Assert.NotNull(workflowDto.Result);
        Assert.Equal((oilLevel * 100).ToString("F2"), workflowDto.Result.Value);
        Assert.Equal(confidence * 100, workflowDto.Result.Confidence);
    }

    [Fact]
    public async Task CheckThatDTOIsCorrectlyFormattedForThermalReadingAnalysis()
    {
        // Arrange
        var record = await _db.NewInspectionRecord(
            blobName: "test",
            inspectionType: "thermal-reading",
            tag: "test-tag",
            inspectionDescription: "test-descr"
        );
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "thermal-reading");
        workflow.Status = WorkflowStatus.Succeeded;
        const float temperature = 42.5f;
        const float confidence = 0.93f;
        workflow.ResultJson = JsonSerializer.Serialize(
            new
            {
                temperature = temperature,
                confidence = confidence,
                warning = (string?)null,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        string Url = $"/api/workflow/id/{workflow.Id}";
        var response = await Client.GetAsync(Url, TestContext.Current.CancellationToken);

        var jsonOptions = new JsonSerializerOptions();
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        jsonOptions.PropertyNameCaseInsensitive = true;
        jsonOptions.IncludeFields = true;

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        var workflowDto = await response.Content.ReadFromJsonAsync<WorkflowDto>(
            jsonOptions,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(workflowDto);
        Assert.Equal(workflowDto.Id, workflow.Id);
        Assert.NotNull(workflowDto.Result);
        Assert.Equal(temperature.ToString("F2"), workflowDto.Result.Value);
        Assert.Equal(confidence * 100, workflowDto.Result.Confidence);
    }

    [Fact]
    public async Task CheckThatDTOIsCorrectlyFormattedForEmptyAnalysis()
    {
        // Arrange
        var record = await _db.NewInspectionRecord(
            blobName: "test",
            inspectionType: "thermal-reading",
            tag: "test-tag",
            inspectionDescription: "test-descr"
        );
        var analysis = await _db.NewAnalysis(inspectionRecords: [record]);
        var run = await _db.NewAnalysisRun(analysis);
        var workflow = await _db.NewWorkflow(run, workflowType: "thermal-reading");
        workflow.Status = WorkflowStatus.Succeeded;

        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        string Url = $"/api/workflow/id/{workflow.Id}";
        var response = await Client.GetAsync(Url, TestContext.Current.CancellationToken);

        var jsonOptions = new JsonSerializerOptions();
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        jsonOptions.PropertyNameCaseInsensitive = true;
        jsonOptions.IncludeFields = true;

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        var workflowDto = await response.Content.ReadFromJsonAsync<WorkflowDto>(
            jsonOptions,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(workflowDto);
        Assert.Equal(workflowDto.Id, workflow.Id);
        Assert.Null(workflowDto.Result);
    }
}
