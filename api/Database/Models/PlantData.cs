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

    private DateTime _dateCreated = DateTime.UtcNow;

    [Required]
    public DateTime DateCreated
    {
        get => _dateCreated;
        set => _dateCreated = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }

    public string? Tag { get; set; }

    public string? Coordinates { get; set; }

    public string? InspectionDescription { get; set; }

    private DateTime? _timestamp;
    public DateTime? Timestamp
    {
        get => _timestamp;
        set => _timestamp = value?.Kind == DateTimeKind.Utc ? value : value?.ToUniversalTime();
    }

    [Required]
    public List<AnalysisType> AnalysisToBeRun { get; set; } = [];

    [Required]
    public List<Analysis> Analysis { get; set; } = [];
}
