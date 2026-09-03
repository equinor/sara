using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using api.Services;

namespace Api.Test.Mocks;

public record ArgoCreateRequest(ArgoWorkflowResource Resource)
{
    public string WorkflowName => Resource.Metadata.Name!;

    public IReadOnlyList<ArgoDagTask> Tasks => Resource.Spec!.Templates.Single().Dag.Tasks;

    public string WorkflowTemplateName => Tasks[0].TemplateRef.Name;

    public IReadOnlyDictionary<string, string> Arguments =>
        Tasks[0]
            .Arguments.Parameters.ToDictionary(
                parameter => parameter.Name,
                parameter => parameter.Value!
            );
}

public class FakeArgoWorkflowClient : IArgoWorkflowClient
{
    public List<ArgoCreateRequest> Requests { get; } = [];
    public Exception? CreateException { get; set; }
    public Func<ArgoCreateRequest, Task>? BeforeCreate { get; set; }

    public async Task<CreatedArgoWorkflow> CreateWorkflowAsync(
        ArgoWorkflowResource workflow,
        CancellationToken cancellationToken = default
    )
    {
        if (CreateException is not null)
        {
            throw CreateException;
        }
        var request = new ArgoCreateRequest(workflow);
        if (BeforeCreate is not null)
        {
            await BeforeCreate(request);
        }
        Requests.Add(request);
        return new CreatedArgoWorkflow(workflow.Metadata.Name!, Guid.NewGuid().ToString());
    }

    public Task<ArgoWorkflowSnapshot> ListWorkflowsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new ArgoWorkflowSnapshot([], "1"));

    public async IAsyncEnumerable<ArgoWorkflowResource> WatchWorkflowsAsync(
        string resourceVersion,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        await Task.CompletedTask;
        yield break;
    }
}
