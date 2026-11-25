namespace api.Configurations;

public class BenchmarkOptions
{
    public const string SectionName = "Benchmarks";

    public string StorageAccount { get; set; } = "saradatasets";

    public List<BenchmarkDefinition> Definitions { get; set; } = [];
}

public class BenchmarkDefinition
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Container { get; set; } = "";
    public string WorkflowTemplate { get; set; } = "";
    public string DefaultSplit { get; set; } = "val";
    public List<string> Devices { get; set; } = ["cpu", "gpu"];
}
