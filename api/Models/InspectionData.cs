using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

#pragma warning disable CS8618
namespace api.Models;

public enum WorkflowStatus
{
    NotStarted,
    Started,
    ExitSuccess,
    ExitFailure
}

[Owned]
public class BlobStorageLocation
{
    [Required]
    public string StorageAccount { get; set; }
    [Required]
    public string BlobContainer { get; set; }
    [Required]
    public string BlobName { get; set; }
}

public class InspectionData
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }

    [Required]
    public string InspectionId { get; set; }

    [Required]
    public BlobStorageLocation RawDataBlobStorageLocation { get; set; }

    [Required]
    public BlobStorageLocation AnonymizedBlobStorageLocation { get; set; }

    [Required]
    public string InstallationCode { get; set; }

    [Required]
    public WorkflowStatus AnonymizerWorkflowStatus { get; set; } = WorkflowStatus.NotStarted;

    [Required]
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    [Required]
    public List<AnalysisType> AnalysisToBeRun { get; set; } = [];

    [Required]
    public List<Analysis> Analysis { get; set; } = [];
}

public class Analysis
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }

    [Required]
    public Uri Uri { get; set; }

    [Required]
    public DateTime DateCreated { get; set; }

    public AnalysisType? Type { get; set; }

    public AnalysisStatus Status { get; set; } = AnalysisStatus.NotStarted;

    public static AnalysisType TypeFromString(string status)
    {
        return status switch
        {
            "anonymize" => AnalysisType.Anonymize,
            _
              => throw new ArgumentException(
                  $"Failed to parse task status '{status}' - not supported"
              )
        };
    }
}

public enum AnalysisStatus
{
    NotStarted,
    Running,
    Completed,
    Failed
}

public enum AnalysisType
{
    Anonymize
}
