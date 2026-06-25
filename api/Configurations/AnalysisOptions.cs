namespace api.Configurations;

public class AnalysisOptions
{
    public const string SectionName = "Analysis";

    public Dictionary<string, AnalysisConfig> Analyses { get; set; } = [];

    public Dictionary<string, WorkflowConfig> Workflows { get; set; } = [];

    public Dictionary<string, List<string>> DefaultAnalysisByFileExtension { get; set; } = [];

    public Dictionary<
        string,
        Dictionary<string, List<string>>
    > DefaultAnalysisByInspectionTypeAndExtension { get; set; } = [];

    public int AnalysisGroupTimeoutMinutes { get; set; } = 30;

    public int AnalysisGroupTimeoutCheckIntervalSeconds { get; set; } = 60;

    public ArgoOptions Argo { get; set; } = new();
}

public class ArgoOptions
{
    public string Namespace { get; set; } = "default";

    public string ServiceAccountName { get; set; } = "robotics-analytics-sa";

    public int WorkflowTtlSecondsAfterCompletion { get; set; } = 604800;

    public int ReconcilerIntervalSeconds { get; set; } = 60;
}

public class AnalysisConfig
{
    public List<string> Workflows { get; set; } = [];
}

public class WorkflowConfig
{
    /// <summary>
    /// Storage account for the primary output blob of this workflow.
    /// Used by <see cref="OutputDescriptor.Derivation"/> = <c>AnalysisRunPath</c>.
    /// </summary>
    public required string OutputStorageAccount { get; set; }

    /// <summary>
    /// File extension (with leading dot) for the primary output blob.
    /// When null, the extension of the workflow's first input is reused.
    /// </summary>
    public string? OutputFileExtension { get; set; }

    public bool IsGate { get; set; }

    public SkipRule? SkipChainIf { get; set; }

    /// <summary>
    /// Argo <c>WorkflowTemplate</c> resource name that hosts this workflow's
    /// <c>run-*</c> template (e.g. <c>anonymizer-workflow-templates</c>).
    /// </summary>
    public required string ArgoWorkflowTemplateName { get; set; }

    /// <summary>
    /// Template name inside <see cref="ArgoWorkflowTemplateName"/> (e.g.
    /// <c>run-anonymizer</c>).
    /// </summary>
    public required string ArgoRunTemplateName { get; set; }

    /// <summary>
    /// Named secondary outputs the workflow produces in addition to its
    /// primary output blob. Each entry derives a blob location at chain-build
    /// time and (optionally) injects it into this step's <c>extras</c> under
    /// <see cref="OutputDescriptor.ExtrasKey"/> so the runtime container can
    /// write to it. Downstream workflows reference these via
    /// <see cref="InputSource"/>.
    /// </summary>
    public Dictionary<string, OutputDescriptor>? Outputs { get; set; }

    /// <summary>
    /// When set, overrides the default "primary output of the previous
    /// non-gate step" input wiring. Lets a step pull from a named output of
    /// any earlier producer (e.g. thermal-reading takes anonymizer's
    /// <c>preProcessed</c> TIFF rather than its primary JPG).
    /// </summary>
    public InputSourceConfig? InputSource { get; set; }
}

public class OutputDescriptor
{
    public required BlobLocationDerivation Derivation { get; set; }

    /// <summary>
    /// File extension (with leading dot) used by
    /// <see cref="BlobLocationDerivation.AnalysisRunPath"/> and
    /// <see cref="BlobLocationDerivation.PrimaryInputWithExtension"/>.
    /// </summary>
    public required string FileExtension { get; set; }

    /// <summary>
    /// When set, the computed location is injected into the producer step's
    /// <c>extras</c> argument under this key (verbatim, no casing
    /// transformation). The runtime container is responsible for writing
    /// the secondary output to that location. When null the output is still
    /// resolvable by downstream <see cref="InputSourceConfig"/> entries but is
    /// not advertised in the producer's argument extras.
    /// </summary>
    public string? ExtrasKey { get; set; }
}

public enum BlobLocationDerivation
{
    /// <summary>
    /// <c>{OutputStorageAccount}/{firstInput.BlobContainer}/analysis-runs/{runId}/{stepNumber}-{workflowType}-{outputName}{FileExtension}</c>.
    /// Matches the primary-output convention but with the output name suffixed.
    /// </summary>
    AnalysisRunPath,

    /// <summary>
    /// <c>{OutputStorageAccount}/{firstInput.BlobContainer}/{firstInput.BlobName-with-extension-replaced}</c>.
    /// Mirrors the legacy <c>AnonymizerPayloadEnricher</c> behaviour of writing
    /// the preProcessed TIFF alongside the raw input under the workflow's own
    /// storage account.
    /// </summary>
    PrimaryInputWithExtension,
}

public class InputSourceConfig
{
    /// <summary>
    /// Workflow type of the upstream step whose output this step consumes.
    /// The chain builder walks back from this step looking for a workflow
    /// matching this type; throws if none found before step 1.
    /// </summary>
    public required string FromPriorWorkflowType { get; set; }

    /// <summary>
    /// Name of the output on the producer step. When null the producer's
    /// primary output is used; otherwise it must match a key in the
    /// producer's <see cref="WorkflowConfig.Outputs"/>.
    /// </summary>
    public string? OutputName { get; set; }
}

public class SkipRule
{
    public required string ResultJsonKeyToCheckForSkipBoolean { get; set; }

    public required string Value { get; set; }
}
