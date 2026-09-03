using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using k8s;
using k8s.Autorest;

namespace api.Services;

/// <summary>Identifies an Argo Workflow created in Kubernetes.</summary>
public record CreatedArgoWorkflow(string Name, string Uid);

/// <summary>
/// Contains the current SARA-managed workflows and the Kubernetes resource version they represent.
/// </summary>
public record ArgoWorkflowSnapshot(
    IReadOnlyList<ArgoWorkflowResource> Items,
    string ResourceVersion
);

/// <summary>Creates, lists, and watches SARA-managed Argo Workflows in Kubernetes.</summary>
public interface IArgoWorkflowClient
{
    /// <summary>Creates a workflow from an Argo WorkflowTemplate.</summary>
    Task<CreatedArgoWorkflow> CreateWorkflowAsync(
        string workflowName,
        string workflowTemplateName,
        Guid workflowId,
        IReadOnlyDictionary<string, string> arguments,
        CancellationToken cancellationToken = default
    );

    /// <summary>Lists SARA-managed workflows and their current resource version.</summary>
    Task<ArgoWorkflowSnapshot> ListWorkflowsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Opens a streaming Kubernetes watch for SARA-managed workflows. The stream emits each
    /// workflow resource when it changes after the supplied resource version and remains open
    /// until Kubernetes closes the connection, an error occurs, or cancellation is requested.
    /// </summary>
    IAsyncEnumerable<ArgoWorkflowResource> WatchWorkflowsAsync(
        string resourceVersion,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// Uses the Kubernetes custom objects API to manage Argo Workflow resources for SARA.
/// </summary>
public class ArgoWorkflowClient(IKubernetes kubernetes, IConfiguration configuration)
    : IArgoWorkflowClient
{
    public const string ManagedByLabel = "app.kubernetes.io/managed-by";
    public const string WorkflowIdLabel = "sara.equinor.com/workflow-id";
    private const string Group = "argoproj.io";
    private const string Version = "v1alpha1";
    private const string Plural = "workflows";
    private const string LabelSelector = ManagedByLabel + "=sara";
    private readonly string _namespace =
        configuration["ArgoWorkflowsNamespace"]
        ?? throw new InvalidOperationException("ArgoWorkflowsNamespace is not configured");

    /// <inheritdoc />
    public async Task<CreatedArgoWorkflow> CreateWorkflowAsync(
        string workflowName,
        string workflowTemplateName,
        Guid workflowId,
        IReadOnlyDictionary<string, string> arguments,
        CancellationToken cancellationToken = default
    )
    {
        var workflowResource = BuildWorkflowResource(
            workflowName,
            workflowTemplateName,
            workflowId,
            arguments
        );

        ArgoWorkflowResource created;
        try
        {
            created =
                await kubernetes.CustomObjects.CreateNamespacedCustomObjectAsync<ArgoWorkflowResource>(
                    workflowResource,
                    Group,
                    Version,
                    _namespace,
                    Plural,
                    cancellationToken: cancellationToken
                );
        }
        catch (HttpOperationException ex)
            when (ex.Response?.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            created =
                await kubernetes.CustomObjects.GetNamespacedCustomObjectAsync<ArgoWorkflowResource>(
                    Group,
                    Version,
                    _namespace,
                    Plural,
                    workflowName,
                    cancellationToken
                );
        }
        return new CreatedArgoWorkflow(
            created.Metadata.Name
                ?? throw new InvalidOperationException("Created Workflow has no name"),
            created.Metadata.Uid
                ?? throw new InvalidOperationException("Created Workflow has no UID")
        );
    }

    /// <inheritdoc />
    public async Task<ArgoWorkflowSnapshot> ListWorkflowsAsync(CancellationToken cancellationToken)
    {
        var list = await kubernetes.CustomObjects.ListNamespacedCustomObjectAsync<ArgoWorkflowList>(
            Group,
            Version,
            _namespace,
            Plural,
            labelSelector: LabelSelector,
            cancellationToken: cancellationToken
        );
        return new ArgoWorkflowSnapshot(list.Items, list.Metadata.ResourceVersion ?? "0");
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ArgoWorkflowResource> WatchWorkflowsAsync(
        string resourceVersion,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var response =
            kubernetes.CustomObjects.ListNamespacedCustomObjectWithHttpMessagesAsync<ArgoWorkflowList>(
                Group,
                Version,
                _namespace,
                Plural,
                labelSelector: LabelSelector,
                resourceVersion: resourceVersion,
                watch: true,
                cancellationToken: cancellationToken
            );
#pragma warning disable CS0618 // KubernetesClient 19 exposes custom-object watches through this API.
        await foreach (
            var watchEvent in response.WatchAsync<ArgoWorkflowResource, ArgoWorkflowList>(
                cancellationToken: cancellationToken
            )
        )
#pragma warning restore CS0618
        {
            if (watchEvent.Item1 == WatchEventType.Error)
            {
                throw new ArgoWorkflowWatchException(
                    watchEvent.Item2.Code,
                    watchEvent.Item2.Message ?? "Kubernetes Workflow watch returned an error"
                );
            }
            yield return watchEvent.Item2;
        }
    }

    /// <summary>Builds the Kubernetes resource used to create an Argo Workflow.</summary>
    private static ArgoWorkflowResource BuildWorkflowResource(
        string workflowName,
        string workflowTemplateName,
        Guid workflowId,
        IReadOnlyDictionary<string, string> arguments
    ) =>
        new()
        {
            Metadata = new ArgoObjectMetadata
            {
                Name = workflowName,
                Labels = new Dictionary<string, string>
                {
                    [ManagedByLabel] = "sara",
                    [WorkflowIdLabel] = workflowId.ToString(),
                },
            },
            Spec = new ArgoWorkflowSpec
            {
                WorkflowTemplateRef = new ArgoWorkflowTemplateRef { Name = workflowTemplateName },
                Arguments = new ArgoArguments
                {
                    Parameters = arguments
                        .Select(pair => new ArgoParameter { Name = pair.Key, Value = pair.Value })
                        .ToList(),
                },
            },
        };
}

public class ArgoWorkflowResource
{
    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; } = "argoproj.io/v1alpha1";

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "Workflow";

    [JsonPropertyName("metadata")]
    public ArgoObjectMetadata Metadata { get; set; } = new();

    [JsonPropertyName("spec")]
    public ArgoWorkflowSpec? Spec { get; set; }

    [JsonPropertyName("status")]
    public ArgoWorkflowStatus? Status { get; set; }

    [JsonPropertyName("code")]
    public int? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class ArgoWorkflowList
{
    [JsonPropertyName("metadata")]
    public ArgoObjectMetadata Metadata { get; set; } = new();

    [JsonPropertyName("items")]
    public List<ArgoWorkflowResource> Items { get; set; } = [];
}

public class ArgoObjectMetadata
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("uid")]
    public string? Uid { get; set; }

    [JsonPropertyName("resourceVersion")]
    public string? ResourceVersion { get; set; }

    [JsonPropertyName("labels")]
    public Dictionary<string, string> Labels { get; set; } = [];
}

public class ArgoWorkflowWatchException(int? statusCode, string message) : Exception(message)
{
    public int? StatusCode { get; } = statusCode;
}

public class ArgoWorkflowSpec
{
    [JsonPropertyName("workflowTemplateRef")]
    public required ArgoWorkflowTemplateRef WorkflowTemplateRef { get; set; }

    [JsonPropertyName("arguments")]
    public required ArgoArguments Arguments { get; set; }
}

public class ArgoWorkflowTemplateRef
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }
}

public class ArgoArguments
{
    [JsonPropertyName("parameters")]
    public List<ArgoParameter> Parameters { get; set; } = [];
}

public class ArgoParameter
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("value")]
    public required string Value { get; set; }
}

public class ArgoWorkflowStatus
{
    [JsonPropertyName("phase")]
    public string? Phase { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("outputs")]
    public ArgoOutputs? Outputs { get; set; }
}

public class ArgoOutputs
{
    [JsonPropertyName("parameters")]
    public List<ArgoParameter> Parameters { get; set; } = [];
}
