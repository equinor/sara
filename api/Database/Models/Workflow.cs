using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

#pragma warning disable CS8618
namespace api.Database.Models;

public enum WorkflowStatus
{
    Pending,
    InProgress,
    Succeeded,
    Failed,
    Skipped,
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

    public override string ToString() => $"{StorageAccount}/{BlobContainer}/{BlobName}";

    public BlobStorageLocation Clone() =>
        new()
        {
            StorageAccount = StorageAccount,
            BlobContainer = BlobContainer,
            BlobName = BlobName,
        };
}

public class Workflow
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    public Guid AnalysisRunId { get; set; }

    [ForeignKey(nameof(AnalysisRunId))]
    public required AnalysisRun AnalysisRun { get; set; }

    [Required]
    public int StepNumber { get; set; }

    [Required]
    public required string WorkflowType { get; set; }

    [Required]
    public required List<BlobStorageLocation> InputBlobStorageLocations { get; set; }

    public WorkflowStatus Status { get; set; } = WorkflowStatus.Pending;

    /// <summary>
    /// Name of the underlying Argo Workflow resource. Combined with the
    /// SARA-configured Argo Workflows base URL and namespace to build a deep link
    /// to the Argo UI.
    /// </summary>
    public string? ArgoWorkflowName { get; set; }

    public string? ArgoWorkflowUid { get; set; }

    public string? ArgoNodeId { get; set; }

    public BlobStorageLocation? OutputBlobStorageLocation { get; set; }

    public string? ResultJson { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? ErrorMessage { get; set; }
}
