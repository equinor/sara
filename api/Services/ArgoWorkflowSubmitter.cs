using System.Text.Json;
using System.Text.Json.Nodes;
using api.Configurations;
using k8s;
using k8s.Autorest;
using Microsoft.Extensions.Options;

namespace api.Services;

/// <summary>
/// Thin wrapper around the official Kubernetes .NET client for submitting and
/// listing Argo <c>Workflow</c> custom resources
/// (<c>argoproj.io/v1alpha1/workflows</c>) in the configured namespace.
/// Authentication uses in-cluster service-account credentials when running in
/// a pod, falling back to the local <c>~/.kube/config</c> default context for
/// host-process deployments (e.g. local-orchestration via <c>dotnet watch</c>).
/// </summary>
public interface IArgoWorkflowSubmitter
{
    /// <summary>Submit a Workflow CR. Returns the created object's metadata.name.</summary>
    Task<string> SubmitAsync(JsonObject manifest, CancellationToken ct = default);

    /// <summary>
    /// List Workflow CRs in the configured namespace matching the given label
    /// selector (e.g. <c>sara.equinor.com/analysis-run-id=&lt;guid&gt;</c>),
    /// returning the raw <c>items</c> array. Returns an empty array when no
    /// workflows match.
    /// </summary>
    Task<JsonArray> ListByLabelAsync(string labelSelector, CancellationToken ct = default);

    /// <summary>
    /// Delete every Workflow CR matching the label selector. Used by retry to
    /// remove the prior terminal CR before submitting a partial-chain retry CR
    /// so the reconciler doesn't observe two CRs per AnalysisRun. Missing CRs
    /// (404) are treated as success. Returns the number of CRs deleted.
    /// </summary>
    Task<int> DeleteByLabelAsync(string labelSelector, CancellationToken ct = default);
}

public class ArgoWorkflowSubmitter : IArgoWorkflowSubmitter, IDisposable
{
    private const string Group = "argoproj.io";
    private const string Version = "v1alpha1";
    private const string Plural = "workflows";

    private readonly string _namespace;
    private readonly IKubernetes _client;
    private readonly ILogger<ArgoWorkflowSubmitter> _logger;

    public ArgoWorkflowSubmitter(
        IOptions<AnalysisOptions> analysisOptions,
        ILogger<ArgoWorkflowSubmitter> logger
    )
    {
        _logger = logger;
        var argo = analysisOptions.Value.Argo;
        _namespace = argo.Namespace;

        // BuildDefaultConfig: in-cluster when KUBERNETES_SERVICE_HOST is set,
        // else falls back to KUBECONFIG / ~/.kube/config current-context.
        var config = KubernetesClientConfiguration.BuildDefaultConfig();
        _client = new Kubernetes(config);

        _logger.LogInformation(
            "ArgoWorkflowSubmitter initialised (namespace='{Namespace}', host='{Host}')",
            _namespace,
            config.Host
        );
    }

    public async Task<string> SubmitAsync(JsonObject manifest, CancellationToken ct = default)
    {
        // The custom-objects API expects an opaque object body; deserializing
        // through JsonElement keeps property casing exactly as authored.
        var body = JsonSerializer.Deserialize<JsonElement>(manifest.ToJsonString());

        var result = await _client.CustomObjects.CreateNamespacedCustomObjectAsync(
            body: body,
            group: Group,
            version: Version,
            namespaceParameter: _namespace,
            plural: Plural,
            cancellationToken: ct
        );

        var name = ExtractMetadataName(result);
        _logger.LogInformation(
            "Submitted Argo Workflow '{Name}' in namespace '{Namespace}'",
            name,
            _namespace
        );
        return name;
    }

    public async Task<JsonArray> ListByLabelAsync(
        string labelSelector,
        CancellationToken ct = default
    )
    {
        try
        {
            var result = await _client.CustomObjects.ListNamespacedCustomObjectAsync(
                group: Group,
                version: Version,
                namespaceParameter: _namespace,
                plural: Plural,
                labelSelector: labelSelector,
                cancellationToken: ct
            );

            var json = JsonSerializer.SerializeToNode(result)?.AsObject();
            if (json is null || !json.TryGetPropertyValue("items", out var items) || items is null)
            {
                return [];
            }
            return items.AsArray();
        }
        catch (HttpOperationException ex) when ((int)ex.Response.StatusCode == 404)
        {
            // CRD not installed; treat as empty rather than crashing the reconciler loop.
            _logger.LogWarning(
                "Argo workflows CRD not found in namespace '{Namespace}' — returning empty list",
                _namespace
            );
            return [];
        }
    }

    public async Task<int> DeleteByLabelAsync(string labelSelector, CancellationToken ct = default)
    {
        var items = await ListByLabelAsync(labelSelector, ct);
        var deleted = 0;
        foreach (var item in items)
        {
            if (item is not JsonObject wf)
            {
                continue;
            }
            var name = wf["metadata"]?["name"]?.GetValue<string>();
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }
            try
            {
                await _client.CustomObjects.DeleteNamespacedCustomObjectAsync(
                    group: Group,
                    version: Version,
                    namespaceParameter: _namespace,
                    plural: Plural,
                    name: name,
                    cancellationToken: ct
                );
                deleted++;
                _logger.LogInformation(
                    "Deleted Argo Workflow '{Name}' in namespace '{Namespace}'",
                    name,
                    _namespace
                );
            }
            catch (HttpOperationException ex) when ((int)ex.Response.StatusCode == 404)
            {
                // Already gone.
            }
        }
        return deleted;
    }

    private static string ExtractMetadataName(object created)
    {
        var node = JsonSerializer.SerializeToNode(created)?.AsObject();
        var name = node?["metadata"]?["name"]?.GetValue<string>();
        return name ?? "<unknown>";
    }

    public void Dispose() => _client.Dispose();
}
