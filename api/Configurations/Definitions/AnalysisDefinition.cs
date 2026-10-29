namespace api.Configurations;

public enum InputSourceKind
{
    InspectionImages,
    ReferenceMetadata,
    InspectionMetadata,
}

public static class ReferenceMetadataFields
{
    public const string ReferenceImage = "ReferenceImageBlobStorageLocation";
    public const string Polygon = "Polygon";
}

public class AnalysisDefinition
{
    public string Name { get; set; } = "";

    public List<StepDefinition> Steps { get; set; } = [];
}

public class StepDefinition
{
    public string Id { get; set; } = "";

    public string Workflow { get; set; } = "";

    public Dictionary<string, InputBinding> Inputs { get; set; } = [];
}

public class InputBinding
{
    public InputSourceKind? Source { get; set; }

    public string? Field { get; set; }

    public string? FromStep { get; set; }

    public string? Output { get; set; }

    public bool Required { get; set; } = true;
}
