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
                ConfidenceThreshold: 0.25,
                BoxPadding: 0.15
            ),
            TestContext.Current.CancellationToken
        );

        Assert.Equal("bounding_box", job.Method);
        Assert.Equal(0.25, job.ConfidenceThreshold);
        Assert.Equal(0.15, job.BoxPadding);

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
        Assert.Null(job.ConfidenceThreshold);
        Assert.Null(job.BoxPadding);

        var parameters = Assert
            .Single(argo.Requests)
            .Resource.Spec!.Arguments!.Parameters.ToDictionary(p => p.Name, p => p.Value ?? "");
        Assert.Equal("segmentation", parameters["method"]);
        Assert.Equal("", parameters["confidence-threshold"]);
        Assert.Equal("", parameters["box-padding"]);
    }

    [Theory]
    [InlineData("segmentation", 0.3, null)]
    [InlineData("segmentation", null, 0.1)]
    [InlineData("bounding_box", 0.0, null)]
    [InlineData("bounding_box", 1.2, null)]
    [InlineData("bounding_box", null, -0.1)]
    [InlineData("bounding_box", null, 1.5)]
    [InlineData("unknown_method", null, null)]
    public async Task TriggerBenchmarkRejectsInvalidMethodOptions(
        string method,
        double? confidenceThreshold,
        double? boxPadding
    )
    {
        var (service, _) = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.TriggerBenchmarkAsync(
                new TriggerBenchmarkRequest(
                    DatasetId: "anonymizer",
                    Version: "v001",
                    Split: "val",
                    Device: "gpu",
                    RunId: null,
                    Method: method,
                    ConfidenceThreshold: confidenceThreshold,
                    BoxPadding: boxPadding
                ),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task GetJobsReadsMethodAndDetectorParametersFromArgoWorkflow()
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
        Assert.Equal(0.3, job.ConfidenceThreshold);
        Assert.Equal(0.1, job.BoxPadding);
    }
}
