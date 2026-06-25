using System.Text.Json;
using System.Text.Json.Nodes;
using api.Configurations;
using api.Database.Models;
using Microsoft.Extensions.Options;

namespace api.Services;

/// <summary>
/// Builds Argo <c>Workflow</c> custom-resource manifests that orchestrate
/// entire <see cref="AnalysisRun"/>s as a single <c>steps</c> chain. Each SARA
/// <see cref="Workflow"/> step expands into four sub-steps:
/// <list type="number">
///   <item><description><c>step-N-notify-started</c> &#8594; PUT /workflow/{id}/started</description></item>
///   <item><description><c>step-N-run</c> &#8594; <c>templateRef</c> to the per-type <c>run-*</c> template</description></item>
///   <item><description><c>step-N-notify-result</c> &#8594; PUT /workflow/{id}/result with the run's <c>result</c> output</description></item>
///   <item><description><c>step-N-notify-exited</c> &#8594; PUT /workflow/{id}/exited with status=Succeeded</description></item>
/// </list>
/// Gates emit a native Argo <c>when:</c> guard on every downstream
/// <c>run</c>/<c>notify-*</c> sub-step. Skipped steps surface as Argo
/// <c>Omitted</c> nodes and are reconciled to DB <c>Skipped</c> by
/// <c>ArgoWorkflowReconciler</c>.
/// </summary>
public interface IWorkflowGraphBuilder
{
    /// <summary>
    /// Compute the primary <see cref="BlobStorageLocation"/> for every
    /// workflow in the chain and assign it to
    /// <see cref="Workflow.OutputBlobStorageLocation"/>. Inputs of step N+1
    /// are derived from the resolved input/output graph (see
    /// <see cref="WorkflowConfig.InputSource"/>). Called once per run during
    /// initial submission.
    /// </summary>
    void ComputeBlobLocations(
        AnalysisRun run,
        IReadOnlyList<Workflow> orderedWorkflows,
        IReadOnlyList<BlobStorageLocation> initialInputs
    );

    /// <summary>
    /// Build the Argo Workflow CR manifest for the full run.
    /// <paramref name="extrasByWorkflowId"/> contains per-step extras as
    /// produced by <see cref="api.Utilities.ITriggerPayloadEnricher"/>; the
    /// builder merges in any <see cref="OutputDescriptor.ExtrasKey"/>
    /// entries.
    /// </summary>
    JsonObject Build(
        AnalysisRun run,
        string analysisName,
        IReadOnlyList<Workflow> orderedWorkflows,
        IReadOnlyDictionary<Guid, Dictionary<string, object>> extrasByWorkflowId
    );

    /// <summary>
    /// Build a partial Argo Workflow CR containing only steps with
    /// <c>StepNumber &gt;= fromStepNumber</c>. Used by retry: input wiring
    /// for the first emitted step comes from the workflow row's persisted
    /// <see cref="Workflow.InputBlobStorageLocations"/> rather than a prior
    /// step's outputs.
    /// </summary>
    JsonObject BuildFromStep(
        AnalysisRun run,
        string analysisName,
        IReadOnlyList<Workflow> orderedWorkflows,
        IReadOnlyDictionary<Guid, Dictionary<string, object>> extrasByWorkflowId,
        int fromStepNumber
    );
}

public class WorkflowGraphBuilder(IOptions<AnalysisOptions> analysisOptions) : IWorkflowGraphBuilder
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly AnalysisOptions _options = analysisOptions.Value;

    public void ComputeBlobLocations(
        AnalysisRun run,
        IReadOnlyList<Workflow> orderedWorkflows,
        IReadOnlyList<BlobStorageLocation> initialInputs
    )
    {
        if (orderedWorkflows.Count == 0)
        {
            return;
        }
        if (initialInputs.Count == 0)
        {
            throw new InvalidOperationException(
                $"Cannot compute blob locations for AnalysisRun {run.Id}: no initial inputs"
            );
        }

        var primaryByStep = new Dictionary<int, BlobStorageLocation>();
        var namedOutputsByStep = new Dictionary<int, Dictionary<string, BlobStorageLocation>>();

        // Primary input wiring: by default, step K consumes the last non-gate
        // step's primary output. Gates are pass-through; their input also
        // tracks the same baseline.
        var currentPrimaryInputs = initialInputs.ToList();

        for (var i = 0; i < orderedWorkflows.Count; i++)
        {
            var step = orderedWorkflows[i];
            var cfg = RequireConfig(step.WorkflowType, step.Id);

            // Resolve this step's input. InputSource overrides the default.
            IReadOnlyList<BlobStorageLocation> stepInputs;
            if (cfg.InputSource is { } src)
            {
                stepInputs =
                [
                    ResolveInputFromSource(
                        src,
                        orderedWorkflows,
                        i,
                        primaryByStep,
                        namedOutputsByStep,
                        step
                    ),
                ];
            }
            else
            {
                stepInputs = currentPrimaryInputs;
            }
            step.InputBlobStorageLocations = stepInputs.Select(b => b.Clone()).ToList();

            // Primary output.
            var firstInput = stepInputs[0];
            var primaryExtension =
                cfg.OutputFileExtension ?? Path.GetExtension(firstInput.BlobName);
            var primary = new BlobStorageLocation
            {
                StorageAccount = cfg.OutputStorageAccount,
                BlobContainer = firstInput.BlobContainer,
                BlobName =
                    $"analysis-runs/{run.Id}/{step.StepNumber}-{step.WorkflowType}{primaryExtension}",
            };
            step.OutputBlobStorageLocation = primary;
            primaryByStep[step.StepNumber] = primary;

            // Named secondary outputs.
            if (cfg.Outputs is { Count: > 0 } outs)
            {
                var named = new Dictionary<string, BlobStorageLocation>(StringComparer.Ordinal);
                foreach (var (name, desc) in outs)
                {
                    named[name] = DeriveNamedOutput(cfg, desc, firstInput, step, name, run.Id);
                }
                namedOutputsByStep[step.StepNumber] = named;
            }

            if (!cfg.IsGate)
            {
                currentPrimaryInputs = [primary];
            }
        }
    }

    public JsonObject Build(
        AnalysisRun run,
        string analysisName,
        IReadOnlyList<Workflow> orderedWorkflows,
        IReadOnlyDictionary<Guid, Dictionary<string, object>> extrasByWorkflowId
    ) => BuildInternal(run, analysisName, orderedWorkflows, extrasByWorkflowId, fromStepNumber: 0);

    public JsonObject BuildFromStep(
        AnalysisRun run,
        string analysisName,
        IReadOnlyList<Workflow> orderedWorkflows,
        IReadOnlyDictionary<Guid, Dictionary<string, object>> extrasByWorkflowId,
        int fromStepNumber
    ) => BuildInternal(run, analysisName, orderedWorkflows, extrasByWorkflowId, fromStepNumber);

    private JsonObject BuildInternal(
        AnalysisRun run,
        string analysisName,
        IReadOnlyList<Workflow> orderedWorkflows,
        IReadOnlyDictionary<Guid, Dictionary<string, object>> extrasByWorkflowId,
        int fromStepNumber
    )
    {
        if (orderedWorkflows.Count == 0)
        {
            throw new InvalidOperationException(
                $"Cannot build Argo Workflow for AnalysisRun {run.Id}: no Workflows provided"
            );
        }

        var argo = _options.Argo;
        var stepsArray = new JsonArray();

        // Active gate guards that apply to all subsequent emitted steps.
        // Each gate adds an expression; we AND them so multi-gate chains
        // are supported (matches legacy WorkflowService behaviour, which
        // skipped everything past the first matching gate).
        var activeGateWhens = new List<string>();

        // Track which steps were emitted so notify-result references resolve.
        var emittedSteps = new HashSet<int>();

        foreach (var step in orderedWorkflows)
        {
            if (step.StepNumber < fromStepNumber)
            {
                continue;
            }

            var cfg = RequireConfig(step.WorkflowType, step.Id);

            if (step.OutputBlobStorageLocation is null)
            {
                throw new InvalidOperationException(
                    $"Workflow {step.Id} ({step.WorkflowType}) has no OutputBlobStorageLocation; "
                        + "call ComputeBlobLocations before Build."
                );
            }

            var workflowIdStr = step.Id.ToString();
            var rawExtras = extrasByWorkflowId.TryGetValue(step.Id, out var e)
                ? new Dictionary<string, object>(e)
                : [];

            // Inject ExtrasKey-bearing named outputs.
            if (cfg.Outputs is { Count: > 0 } outs)
            {
                foreach (var (name, desc) in outs)
                {
                    if (string.IsNullOrWhiteSpace(desc.ExtrasKey))
                    {
                        continue;
                    }
                    rawExtras[desc.ExtrasKey] = DeriveNamedOutput(
                        cfg,
                        desc,
                        step.InputBlobStorageLocations.Count > 0
                            ? step.InputBlobStorageLocations[0]
                            : throw new InvalidOperationException(
                                $"Workflow {step.Id} has no InputBlobStorageLocations; cannot derive named output '{name}'"
                            ),
                        step,
                        name,
                        run.Id
                    );
                }
            }

            var guard = activeGateWhens.Count == 0 ? null : string.Join(" && ", activeGateWhens);

            stepsArray.Add(
                BuildParallelStep(
                    ApplyWhen(BuildNotifyStarted(step.StepNumber, workflowIdStr), guard)
                )
            );
            stepsArray.Add(
                BuildParallelStep(
                    ApplyWhen(
                        BuildRun(
                            step.StepNumber,
                            cfg.ArgoWorkflowTemplateName,
                            cfg.ArgoRunTemplateName,
                            step.InputBlobStorageLocations,
                            step.OutputBlobStorageLocation,
                            rawExtras
                        ),
                        guard
                    )
                )
            );
            stepsArray.Add(
                BuildParallelStep(
                    ApplyWhen(BuildNotifyResult(step.StepNumber, workflowIdStr), guard)
                )
            );
            stepsArray.Add(
                BuildParallelStep(
                    ApplyWhen(BuildNotifyExited(step.StepNumber, workflowIdStr), guard)
                )
            );

            emittedSteps.Add(step.StepNumber);

            // After emitting this step, if it's a gate, add its when-expression
            // to the active guards for subsequent steps.
            if (cfg.IsGate && cfg.SkipChainIf is { } rule)
            {
                // Argo expression: run downstream only when gate's result != skipValue.
                // string(fromJSON(...)) coerces booleans / numbers to string for == comparison.
                var resultExpr =
                    $"string(fromJSON(steps['step-{step.StepNumber}-run'].outputs.parameters.result)['{rule.ResultJsonKeyToCheckForSkipBoolean}'])";
                activeGateWhens.Add($"{resultExpr} != '{rule.Value}'");
            }
        }

        return new JsonObject
        {
            ["apiVersion"] = "argoproj.io/v1alpha1",
            ["kind"] = "Workflow",
            ["metadata"] = new JsonObject
            {
                ["generateName"] = $"analysis-run-{Sanitize(analysisName)}-",
                ["namespace"] = argo.Namespace,
                ["labels"] = new JsonObject
                {
                    ["sara.equinor.com/analysis-run-id"] = run.Id.ToString(),
                    ["sara.equinor.com/analysis-id"] = run.AnalysisId.ToString(),
                    ["sara.equinor.com/analysis-name"] = Sanitize(analysisName),
                },
            },
            ["spec"] = new JsonObject
            {
                ["serviceAccountName"] = argo.ServiceAccountName,
                ["entrypoint"] = "main",
                ["ttlStrategy"] = new JsonObject
                {
                    ["secondsAfterCompletion"] = argo.WorkflowTtlSecondsAfterCompletion,
                },
                ["templates"] = new JsonArray
                {
                    new JsonObject { ["name"] = "main", ["steps"] = stepsArray },
                },
            },
        };
    }

    private WorkflowConfig RequireConfig(string workflowType, Guid workflowId)
    {
        if (!_options.Workflows.TryGetValue(workflowType, out var cfg))
        {
            throw new InvalidOperationException(
                $"Unknown workflow type '{workflowType}' for Workflow {workflowId}"
            );
        }
        return cfg;
    }

    private BlobStorageLocation ResolveInputFromSource(
        InputSourceConfig src,
        IReadOnlyList<Workflow> orderedWorkflows,
        int consumerIndex,
        IReadOnlyDictionary<int, BlobStorageLocation> primaryByStep,
        IReadOnlyDictionary<int, Dictionary<string, BlobStorageLocation>> namedOutputsByStep,
        Workflow consumer
    )
    {
        for (var j = consumerIndex - 1; j >= 0; j--)
        {
            var candidate = orderedWorkflows[j];
            if (
                !string.Equals(
                    candidate.WorkflowType,
                    src.FromPriorWorkflowType,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                continue;
            }
            if (string.IsNullOrEmpty(src.OutputName))
            {
                return primaryByStep[candidate.StepNumber];
            }
            if (
                !namedOutputsByStep.TryGetValue(candidate.StepNumber, out var named)
                || !named.TryGetValue(src.OutputName, out var loc)
            )
            {
                throw new InvalidOperationException(
                    $"Workflow {consumer.Id} ({consumer.WorkflowType}) declares InputSource "
                        + $"FromPriorWorkflowType='{src.FromPriorWorkflowType}' OutputName='{src.OutputName}', "
                        + $"but producer at step {candidate.StepNumber} has no such named output."
                );
            }
            return loc;
        }
        throw new InvalidOperationException(
            $"Workflow {consumer.Id} ({consumer.WorkflowType}) declares InputSource "
                + $"FromPriorWorkflowType='{src.FromPriorWorkflowType}', but no upstream step of that type exists."
        );
    }

    private static BlobStorageLocation DeriveNamedOutput(
        WorkflowConfig cfg,
        OutputDescriptor desc,
        BlobStorageLocation firstInput,
        Workflow step,
        string outputName,
        Guid runId
    )
    {
        return desc.Derivation switch
        {
            BlobLocationDerivation.AnalysisRunPath => new BlobStorageLocation
            {
                StorageAccount = cfg.OutputStorageAccount,
                BlobContainer = firstInput.BlobContainer,
                BlobName =
                    $"analysis-runs/{runId}/{step.StepNumber}-{step.WorkflowType}-{outputName}{desc.FileExtension}",
            },
            BlobLocationDerivation.PrimaryInputWithExtension => new BlobStorageLocation
            {
                StorageAccount = cfg.OutputStorageAccount,
                BlobContainer = firstInput.BlobContainer,
                BlobName = ReplaceFileEnding(firstInput.BlobName, desc.FileExtension),
            },
            _ => throw new InvalidOperationException(
                $"Unknown BlobLocationDerivation '{desc.Derivation}' for output '{outputName}' on workflow type '{step.WorkflowType}'"
            ),
        };
    }

    private static string ReplaceFileEnding(string blobName, string newExtension)
    {
        var lastDot = blobName.LastIndexOf('.');
        var lastSlash = blobName.LastIndexOf('/');
        if (lastDot <= lastSlash)
        {
            return blobName + newExtension;
        }
        return blobName[..lastDot] + newExtension;
    }

    private static JsonArray BuildParallelStep(JsonObject step) => new() { step };

    private static JsonObject ApplyWhen(JsonObject step, string? whenExpr)
    {
        if (!string.IsNullOrEmpty(whenExpr))
        {
            step["when"] = whenExpr;
        }
        return step;
    }

    private static JsonObject BuildNotifyStarted(int stepNumber, string workflowId) =>
        new()
        {
            ["name"] = $"step-{stepNumber}-notify-started",
            ["templateRef"] = new JsonObject
            {
                ["name"] = "workflow-notifier",
                ["template"] = "notify-started",
            },
            ["arguments"] = new JsonObject
            {
                ["parameters"] = new JsonArray { Param("workflowId", workflowId) },
            },
        };

    private static JsonObject BuildRun(
        int stepNumber,
        string templateName,
        string runTemplateName,
        IReadOnlyList<BlobStorageLocation> inputs,
        BlobStorageLocation output,
        IReadOnlyDictionary<string, object> extras
    ) =>
        new()
        {
            ["name"] = $"step-{stepNumber}-run",
            ["templateRef"] = new JsonObject
            {
                ["name"] = templateName,
                ["template"] = runTemplateName,
            },
            ["arguments"] = new JsonObject
            {
                ["parameters"] = new JsonArray
                {
                    Param("inputBlobStorageLocations", SerializeAsJsonString(inputs)),
                    Param("outputBlobStorageLocation", SerializeAsJsonString(output)),
                    Param("extras", SerializeAsJsonString(extras)),
                },
            },
        };

    private static JsonObject BuildNotifyResult(int stepNumber, string workflowId) =>
        new()
        {
            ["name"] = $"step-{stepNumber}-notify-result",
            ["templateRef"] = new JsonObject
            {
                ["name"] = "workflow-notifier",
                ["template"] = "notify-result",
            },
            ["arguments"] = new JsonObject
            {
                ["parameters"] = new JsonArray
                {
                    Param("workflowId", workflowId),
                    Param(
                        "result",
                        $"{{{{steps.step-{stepNumber}-run.outputs.parameters.result}}}}"
                    ),
                },
            },
        };

    private static JsonObject BuildNotifyExited(int stepNumber, string workflowId) =>
        new()
        {
            ["name"] = $"step-{stepNumber}-notify-exited",
            ["templateRef"] = new JsonObject
            {
                ["name"] = "workflow-notifier",
                ["template"] = "notify-exited",
            },
            ["arguments"] = new JsonObject
            {
                ["parameters"] = new JsonArray
                {
                    Param("workflowId", workflowId),
                    Param("status", "Succeeded"),
                    Param("failures", ""),
                },
            },
        };

    private static JsonObject Param(string name, string value) =>
        new() { ["name"] = name, ["value"] = value };

    private static string SerializeAsJsonString(object value) =>
        JsonSerializer.Serialize(value, CamelCase);

    /// <summary>
    /// K8s resource names must be lowercase alphanumerics or hyphens. Map any
    /// other character to '-' and trim leading/trailing hyphens.
    /// </summary>
    private static string Sanitize(string input)
    {
        var chars = input
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-')
            .ToArray();
        var sanitized = new string(chars).Trim('-');
        return string.IsNullOrEmpty(sanitized) ? "run" : sanitized;
    }
}
