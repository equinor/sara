using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#pragma warning disable CS8618
namespace api.Database.Models;

public class Analysis
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }

    [Required]
    public BlobStorageLocation SourcePath { get; set; }

    public BlobStorageLocation? DestinationPath { get; set; }

    [Required]
    public DateTime DateCreated { get; set; }

    [Required]
    public AnalysisType Type { get; set; }

    public AnalysisStatus Status { get; set; } = AnalysisStatus.NotStarted;

    public static AnalysisType? TypeFromString(string? status)
    {
        if (string.IsNullOrEmpty(status))
            return null;
        status = status.ToLowerInvariant();
        return status switch
        {
            "anonymizer" => AnalysisType.Anonymizer,
            "constantleveloiler" => AnalysisType.ConstantLevelOiler,
            _ => null,
        };
    }

    public static string? TypeToString(AnalysisType type)
    {
        return type switch
        {
            AnalysisType.Anonymizer => "anonymizer",
            AnalysisType.ConstantLevelOiler => "constantleveloiler",
            _ => null,
        };
    }
}

public enum AnalysisStatus
{
    NotStarted,
    Running,
    Completed,
    Failed,
}

public enum AnalysisType
{
    Anonymizer,
    ConstantLevelOiler,
}
