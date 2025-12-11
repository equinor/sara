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
    public required string StorageAccount { get; set; }

    [Required]
    public required string BlobContainer { get; set; }

    [Required]
    public required string BlobName { get; set; }
}

public abstract class Workflow
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }

    [Required]
    public required BlobStorageLocation SourceBlobStorageLocation { get; set; }

    [Required]
    public required BlobStorageLocation DestinationBlobStorageLocation { get; set; }

    [Required]
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    public WorkflowStatus Status { get; set; } = WorkflowStatus.NotStarted;
}

public class Anonymization : Workflow
{
    public bool? IsPersonInImage { get; set; }
}

public class CLOEAnalysis : Workflow
{
    public float? OilLevel { get; set; }
}

public class FencillaAnalysis : Workflow
{
    public bool? IsBreak { get; set; }
    public float? Confidence { get; set; }
}

public class ThermalReadingAnalysis : Workflow
{
    public float? Temperature { get; set; }
}
