namespace api.Configurations;

public class BenchmarkOptions
{
    public const string SectionName = "Benchmarks";
    public const long DefaultCacheSizeLimitBytes = 64 * 1024 * 1024;
    public const int DefaultCacheEntryTtlMinutes = 10;

    public string StorageAccount { get; set; } = "saradatasets";
    public long CacheSizeLimitBytes { get; set; } = DefaultCacheSizeLimitBytes;
    public int CacheEntryTtlMinutes { get; set; } = DefaultCacheEntryTtlMinutes;

    public List<BenchmarkDefinition> Definitions { get; set; } = [];
}

public class BenchmarkDefinition
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Container { get; set; } = "";
    public string WorkflowTemplate { get; set; } = "";
    public string DefaultSplit { get; set; } = "val";
    public List<string> Devices { get; set; } = [];
    public List<string> Methods { get; set; } = [];
    public List<BenchmarkParameterDefinition> Parameters { get; set; } = [];
}

public class BenchmarkParameterDefinition
{
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public double? Min { get; set; }
    public bool MinExclusive { get; set; }
    public double? Max { get; set; }
    public double? Step { get; set; }
    public string? Placeholder { get; set; }
    public List<string> OnlyForMethods { get; set; } = [];
}
