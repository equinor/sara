using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api.Configurations;
using api.Controllers.Models;
using api.Services;
using Api.Test.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Test.Services;

public class BenchmarkServiceTests
{
    private static (BenchmarkService Service, FakeArgoWorkflowClient Argo) CreateService()
    {
        var argo = new FakeArgoWorkflowClient();
        var options = Options.Create(
            new BenchmarkOptions
            {
                StorageAccount = "saradatasets",
                Definitions =
                [
                    new BenchmarkDefinition
                    {
                        Id = "anonymizer",
                        Label = "Anonymizer",
                        Container = "anonymizer",
                        WorkflowTemplate = "anonymizer-benchmark",
                        DefaultSplit = "val",
                        Devices = ["cpu", "gpu"],
                        Methods = ["segmentation", "bounding_box"],
                        Parameters =
                        [
                            new BenchmarkParameterDefinition
                            {
                                Name = "confidence-threshold",
                                Label = "Confidence",
                                Min = 0.0,
                                MinExclusive = true,
                                Max = 1.0,
                                Step = 0.05,
                                Placeholder = "0.3",
                                OnlyForMethods = ["bounding_box"],
                            },
                            new BenchmarkParameterDefinition
                            {
                                Name = "box-padding",
                                Label = "Box padding",
                                Min = 0.0,
                                Max = 1.0,
                                Step = 0.05,
                                Placeholder = "0.1",
                                OnlyForMethods = ["bounding_box"],
                            },
                        ],
                    },
                    new BenchmarkDefinition
                    {
                        Id = "rain-drop-detection",
                        Label = "Rain Drop Detector",
                        Container = "rain-drop-detection",
                        WorkflowTemplate = "rain-drop-detector-benchmark",
                        DefaultSplit = "val",
                        Devices = ["cpu"],
                        Parameters =
                        [
                            new BenchmarkParameterDefinition
                            {
                                Name = "confidence-threshold",
                                Label = "Confidence",
                                Min = 0.0,
                                MinExclusive = true,
                                Max = 1.0,
                                Step = 0.05,
                                Placeholder = "0.25",
                            },
                        ],
                    },
                ],
            }
        );
        var cache = new BenchmarkIndexCache(options, NullLogger<BenchmarkIndexCache>.Instance);
        var service = new BenchmarkService(
            argo,
            new BlobStorageServiceMock { BlobExists = false },
            cache,
            options,
            NullLogger<BenchmarkService>.Instance
        );
        return (service, argo);
    }

    [Fact]
    public async Task TriggerBenchmarkPassesBoundingBoxParametersToArgo()
    {
        var (service, argo) = CreateService();

        var job = await service.TriggerBenchmarkAsync(
            new TriggerBenchmarkRequest(
                DatasetId: "anonymizer",
                Version: "v001",
                Split: "val",
                Device: "gpu",
                RunId: "rtmdet-run",
                Method: "bounding_box",
                Parameters: new Dictionary<string, double?>
                {
                    ["confidence-threshold"] = 0.25,
                    ["box-padding"] = 0.15,
                }
            ),
            TestContext.Current.CancellationToken
        );

        Assert.Equal("bounding_box", job.Method);
        Assert.Equal("0.25", job.Parameters["confidence-threshold"]);
        Assert.Equal("0.15", job.Parameters["box-padding"]);

        var created = Assert.Single(argo.Requests).Resource;
        var parameters = created.Spec!.Arguments!.Parameters.ToDictionary(
            p => p.Name,
            p => p.Value ?? ""
        );
        Assert.Equal("bounding_box", parameters["method"]);
        Assert.Equal("0.25", parameters["confidence-threshold"]);
        Assert.Equal("0.15", parameters["box-padding"]);
    }

    [Fact]
    public async Task TriggerBenchmarkDefaultsToFirstConfiguredMethod()
    {
        var (service, argo) = CreateService();

        var job = await service.TriggerBenchmarkAsync(
            new TriggerBenchmarkRequest(
                DatasetId: "anonymizer",
                Version: "v001",
                Split: "val",
                Device: "cpu",
                RunId: null
            ),
            TestContext.Current.CancellationToken
        );

        Assert.Equal("segmentation", job.Method);
        Assert.Empty(job.Parameters);

        var parameters = Assert
            .Single(argo.Requests)
            .Resource.Spec!.Arguments!.Parameters.ToDictionary(p => p.Name, p => p.Value ?? "");
        Assert.Equal("segmentation", parameters["method"]);
        Assert.Equal("", parameters["confidence-threshold"]);
        Assert.Equal("", parameters["box-padding"]);
    }

    [Fact]
    public async Task TriggerRainDropBenchmarkSupportsConfidenceOverrideWithoutMethod()
    {
        var (service, argo) = CreateService();

        var job = await service.TriggerBenchmarkAsync(
            new TriggerBenchmarkRequest(
                DatasetId: "rain-drop-detection",
                Version: "v001",
                Split: "val",
                Device: "cpu",
                RunId: "rain-run-1",
                Parameters: new Dictionary<string, double?> { ["confidence-threshold"] = 0.4 }
            ),
            TestContext.Current.CancellationToken
        );

        Assert.Null(job.Method);
        Assert.Equal("0.4", job.Parameters["confidence-threshold"]);

        var created = Assert.Single(argo.Requests).Resource;
        Assert.Equal("rain-drop-detector-benchmark", created.Spec!.WorkflowTemplateRef!.Name);
        var parameters = created.Spec.Arguments!.Parameters.ToDictionary(
            p => p.Name,
            p => p.Value ?? ""
        );
        Assert.False(parameters.ContainsKey("method"));
        Assert.Equal("0.4", parameters["confidence-threshold"]);
        Assert.Equal("cpu", parameters["device"]);
    }

    [Theory]
    [InlineData("anonymizer", "gpu", "segmentation", "confidence-threshold", 0.3)]
    [InlineData("anonymizer", "gpu", "segmentation", "box-padding", 0.1)]
    [InlineData("anonymizer", "gpu", "bounding_box", "confidence-threshold", 0.0)]
    [InlineData("anonymizer", "gpu", "bounding_box", "confidence-threshold", 1.2)]
    [InlineData("anonymizer", "gpu", "bounding_box", "box-padding", -0.1)]
    [InlineData("anonymizer", "gpu", "bounding_box", "box-padding", 1.5)]
    [InlineData("anonymizer", "gpu", "unknown_method", null, null)]
    [InlineData("rain-drop-detection", "gpu", null, null, null)]
    [InlineData("rain-drop-detection", "cpu", "segmentation", null, null)]
    [InlineData("rain-drop-detection", "cpu", null, "confidence-threshold", 0.0)]
    [InlineData("rain-drop-detection", "cpu", null, "box-padding", 0.1)]
    public async Task TriggerBenchmarkRejectsInvalidOptions(
        string datasetId,
        string device,
        string? method,
        string? paramName,
        double? paramValue
    )
    {
        var (service, _) = CreateService();
        var parameters = paramName is not null
            ? new Dictionary<string, double?> { [paramName] = paramValue }
            : null;

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.TriggerBenchmarkAsync(
                new TriggerBenchmarkRequest(
                    DatasetId: datasetId,
                    Version: "v001",
                    Split: "val",
                    Device: device,
                    RunId: null,
                    Method: method,
                    Parameters: parameters
                ),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task GetJobsReadsMethodAndExtraParametersFromArgoWorkflow()
    {
        var (service, argo) = CreateService();
        argo.ListedWorkflows.Add(
            new ArgoWorkflowResource
            {
                Metadata = new ArgoObjectMetadata
                {
                    Name = "anonymizer-benchmark-abc",
                    Labels = new Dictionary<string, string>
                    {
                        [BenchmarkService.DatasetLabel] = "anonymizer",
                    },
                },
                Spec = new ArgoWorkflowSpec
                {
                    Arguments = new ArgoArguments
                    {
                        Parameters =
                        [
                            new ArgoParameter { Name = "version", Value = "v001" },
                            new ArgoParameter { Name = "split", Value = "val" },
                            new ArgoParameter { Name = "device", Value = "gpu" },
                            new ArgoParameter { Name = "method", Value = "bounding_box" },
                            new ArgoParameter { Name = "confidence-threshold", Value = "0.3" },
                            new ArgoParameter { Name = "box-padding", Value = "0.1" },
                        ],
                    },
                },
            }
        );

        var jobs = await service.GetJobsAsync(TestContext.Current.CancellationToken);

        var job = Assert.Single(jobs);
        Assert.Equal("bounding_box", job.Method);
        Assert.Equal("0.3", job.Parameters["confidence-threshold"]);
        Assert.Equal("0.1", job.Parameters["box-padding"]);
    }
}
