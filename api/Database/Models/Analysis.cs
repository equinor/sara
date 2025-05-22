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
    public Uri Uri { get; set; } // Rename

    [Required]
    public BlobStorageLocation SourcePath { get; set; }

    [Required]
    public BlobStorageLocation DestinationPath { get; set; }

    [Required]
    public DateTime DateCreated { get; set; }

    [Required]
    public AnalysisType Type { get; set; }

    public AnalysisStatus Status { get; set; } = AnalysisStatus.NotStarted;

    public Result? Result { get; set; }

    public static AnalysisType? TypeFromString(string status)
    {
        if (string.IsNullOrEmpty(status))
            return null;
        return status switch
        {
            "anonymize" => AnalysisType.Anonymize,
            _ => null,
        };
    }
}

public class Result
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }

    [Required]
    public string Type { get; set; }

    [Required]
    public string Value { get; set; }

    [Range(0, 100)]
    public int? Confidence { get; set; }
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
    Anonymize,
}
