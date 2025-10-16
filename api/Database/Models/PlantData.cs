using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

#pragma warning disable CS8618
namespace api.Database.Models;

public enum WorkflowStatus
{
    NotStarted,
    Started,
    ExitSuccess,
    ExitFailure,
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

public class PlantData
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
    public BlobStorageLocation VisualizedBlobStorageLocation { get; set; }

    [Required]
    public string InstallationCode { get; set; }

    [Required]
    public WorkflowStatus AnonymizerWorkflowStatus { get; set; } = WorkflowStatus.NotStarted; // TODO: Rename this to just WorkflowStatus

    // TODO Add a separate field for Anonomizer done

    [Required]
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    public string? Tag { get; set; }

    public string? Coordinates { get; set; }

    public string? InspectionDescription { get; set; }

    public DateTime? Timestamp { get; set; }

    [Required]
    public List<AnalysisType> AnalysisToBeRun { get; set; } = [];

    [Required]
    public List<Analysis> Analysis { get; set; } = [];
}
