using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using api.Services;
using k8s;
using k8s.Autorest;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Test.Services;

public class ArgoWorkflowClientTests
{
    private const string WorkflowJson = """
        {"apiVersion":"argoproj.io/v1alpha1","kind":"Workflow","metadata":{"name":"analysis-1","uid":"workflow-uid","resourceVersion":"42"}}
        """;
    private const string WorkflowsPath =
        "/apis/argoproj.io/v1alpha1/namespaces/argo-test/workflows";

    [Fact]
    public async Task ListAndWatch_UseSameSelectorExcludingReconciledWorkflows()
    {
        using var handler = new WorkflowHttpHandler
        {
            ResponseBody = """{"metadata":{"resourceVersion":"41"},"items":[]}""",
        };
        using var kubernetes = new Kubernetes(
            new KubernetesClientConfiguration { Host = "http://localhost" },
            handler
        );
        var client = CreateClient(kubernetes);

        var snapshot = await client.ListWorkflowsAsync(TestContext.Current.CancellationToken);
        Assert.Equal("41", snapshot.ResourceVersion);
        handler.ResponseBody = $$"""{"type":"ADDED","object":{{WorkflowJson}}}""" + "\n";
        await using var watch = client
            .WatchWorkflowsAsync(snapshot.ResourceVersion, TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);
        Assert.True(await watch.MoveNextAsync());

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(
            handler.Requests,
            request =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal(WorkflowsPath, request.Uri.AbsolutePath);
                Assert.Equal(
                    "app.kubernetes.io/managed-by=sara,sara.equinor.com/analysis-run-id,sara.equinor.com/reconciled!=true",
                    QueryHelpers.ParseQuery(request.Uri.Query)["labelSelector"].ToString()
                );
            }
        );
        var watchQuery = QueryHelpers.ParseQuery(handler.Requests[1].Uri.Query);
        Assert.Equal("true", watchQuery["watch"].ToString());
        Assert.Equal(snapshot.ResourceVersion, watchQuery["resourceVersion"].ToString());
    }

    [Theory]
    [InlineData("ADDED", false)]
    [InlineData("MODIFIED", false)]
    [InlineData("DELETED", true)]
    public async Task WatchWorkflows_SetsIsDeletedOnlyForDeletedEvents(
        string eventType,
        bool isDeleted
    )
    {
        using var handler = new WorkflowHttpHandler
        {
            ResponseBody = $$"""{"type":"{{eventType}}","object":{{WorkflowJson}}}""" + "\n",
        };
        using var kubernetes = new Kubernetes(
            new KubernetesClientConfiguration { Host = "http://localhost" },
            handler
        );
        var client = CreateClient(kubernetes);

        await using var watch = client
            .WatchWorkflowsAsync("41", TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);
        Assert.True(await watch.MoveNextAsync());

        Assert.Equal(isDeleted, watch.Current.IsDeleted);
        Assert.Equal("analysis-1", watch.Current.Metadata.Name);
        Assert.Equal("workflow-uid", watch.Current.Metadata.Uid);
        Assert.Equal("42", watch.Current.Metadata.ResourceVersion);
    }

    [Fact]
    public async Task MarkWorkflowReconciled_SendsOnlyLabelAndPreconditionsAsMergePatch()
    {
        using var handler = new WorkflowHttpHandler();
        using var kubernetes = new Kubernetes(
            new KubernetesClientConfiguration { Host = "http://localhost" },
            handler
        );
        var client = CreateClient(kubernetes);
        var metadata = new ArgoObjectMetadata
        {
            Name = "analysis-1",
            Uid = "workflow-uid",
            ResourceVersion = "42",
            Labels = new()
            {
                [ArgoWorkflowClient.ManagedByLabel] = "sara",
                [ArgoWorkflowClient.AnalysisRunIdLabel] = "run-1",
                [ArgoWorkflowClient.ReconciledLabel] = "false",
            },
        };

        await client.MarkWorkflowReconciledAsync(metadata, TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Equal(WorkflowsPath + "/analysis-1", request.Uri.AbsolutePath);
        Assert.Equal("application/merge-patch+json", request.MediaType);
        using var body = JsonDocument.Parse(request.Body);
        var rootProperty = Assert.Single(body.RootElement.EnumerateObject());
        Assert.Equal("metadata", rootProperty.Name);
        var patchMetadata = rootProperty.Value;
        Assert.Equal(
            ["labels", "resourceVersion", "uid"],
            patchMetadata.EnumerateObject().Select(property => property.Name).Order()
        );
        Assert.Equal(metadata.Uid, patchMetadata.GetProperty("uid").GetString());
        Assert.Equal(
            metadata.ResourceVersion,
            patchMetadata.GetProperty("resourceVersion").GetString()
        );
        var label = Assert.Single(patchMetadata.GetProperty("labels").EnumerateObject());
        Assert.Equal("sara.equinor.com/reconciled", label.Name);
        Assert.Equal("true", label.Value.GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task MarkWorkflowReconciled_PropagatesHttpFailure(HttpStatusCode statusCode)
    {
        using var handler = new WorkflowHttpHandler
        {
            StatusCode = statusCode,
            ResponseBody =
                $$"""{"kind":"Status","apiVersion":"v1","status":"Failure","code":{{(int)statusCode}}}""",
        };
        using var kubernetes = new Kubernetes(
            new KubernetesClientConfiguration { Host = "http://localhost" },
            handler
        );
        var client = CreateClient(kubernetes);

        var exception = await Assert.ThrowsAsync<HttpOperationException>(() =>
            client.MarkWorkflowReconciledAsync(
                new ArgoObjectMetadata
                {
                    Name = "analysis-1",
                    Uid = "workflow-uid",
                    ResourceVersion = "42",
                },
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(statusCode, exception.Response.StatusCode);
        Assert.Equal(HttpMethod.Patch, Assert.Single(handler.Requests).Method);
    }

    private static ArgoWorkflowClient CreateClient(IKubernetes kubernetes) =>
        new(
            kubernetes,
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?> { ["ArgoWorkflowsNamespace"] = "argo-test" }
                )
                .Build()
        );

    private sealed record RecordedRequest(
        HttpMethod Method,
        Uri Uri,
        string Body,
        string? MediaType
    );

    private sealed class WorkflowHttpHandler : DelegatingHandler
    {
        public List<RecordedRequest> Requests { get; } = [];
        public string ResponseBody { get; set; } = WorkflowJson;
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Requests.Add(
                new RecordedRequest(
                    request.Method,
                    request.RequestUri!,
                    request.Content is null
                        ? string.Empty
                        : await request.Content.ReadAsStringAsync(cancellationToken),
                    request.Content?.Headers.ContentType?.MediaType
                )
            );
            return new HttpResponseMessage(StatusCode)
            {
                RequestMessage = request,
                Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json"),
            };
        }
    }
}
