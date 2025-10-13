using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace api.Database.Models;

public class Analysis
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    public required BlobStorageLocation SourceBlobStorageLocation { get; set; } // Usuallty the output from the Anonymizer

    [Required]
    public required BlobStorageLocation VisualizedBlobStorageLocation { get; set; }

    [Required]
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    [Required]
    public required AnalysisType Type { get; set; }

    public WorkflowStatus Status { get; set; } = WorkflowStatus.NotStarted;

    public static AnalysisType? TypeFromString(string? status)
    {
        if (string.IsNullOrEmpty(status))
            return null;
        status = status.ToLowerInvariant();
        return status switch
        {
            "constantleveloilerestimator" => AnalysisType.ConstantLevelOilerEstimator,
            "fencilla" => AnalysisType.Fencilla,
            _ => null,
        };
    }

    public static string? TypeToString(AnalysisType type)
    {
        return type switch
        {
            AnalysisType.ConstantLevelOilerEstimator => "constantleveloilerestimator",
            AnalysisType.Fencilla => "fencilla",
            _ => null,
        };
    }
}

public enum AnalysisType
{
    ConstantLevelOilerEstimator,
    Fencilla,
}
