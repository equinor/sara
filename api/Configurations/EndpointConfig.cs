public class EndpointConfig
{
    public string DefaultScheme { get; set; } = "https";
    public string? DefaultHost { get; set; }
    public int DefaultPort { get; set; } // 0 means "not set"
}
