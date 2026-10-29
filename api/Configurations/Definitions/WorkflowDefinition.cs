namespace api.Configurations;

public enum WorkflowInputType
{
    BlobStorageLocation,
    BlobStorageLocationList,
    Polygon,
    InspectionMetadataList,
}

public enum OutputNaming
{
    Generated,
    InputName,
}

public class WorkflowDefinition
{
    public string Name { get; set; } = "";

    public string TemplateName { get; set; } = "";

    public Dictionary<string, InputDefinition> Inputs { get; set; } = [];

    public Dictionary<string, OutputDefinition> Outputs { get; set; } = [];

    public List<ArgumentDefinition> Arguments { get; set; } = [];

    public SkipRule? SkipChainIf { get; set; }
}

public class InputDefinition
{
    public WorkflowInputType Type { get; set; }
}

public class OutputDefinition
{
    public string? FileExtension { get; set; }

    public OutputNaming Naming { get; set; } = OutputNaming.Generated;
}

public class ArgumentValue
{
    public string? Input { get; set; }

    public string? Output { get; set; }

    public string? Value { get; set; }

    public Dictionary<string, ArgumentValue>? Object { get; set; }
}

public class ArgumentDefinition : ArgumentValue
{
    public string Name { get; set; } = "";
}

public class SkipRule
{
    public required string ResultJsonKeyToCheckForSkipBoolean { get; set; }

    public required string Value { get; set; }
}
