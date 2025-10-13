using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace api.Database.Models;

[Owned]
public class Anonymization
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public WorkflowStatus Status { get; set; } = WorkflowStatus.NotStarted;

    public bool IsAnonymized { get; set; } = false;

    [Required]
    public required BlobStorageLocation RawDataBlobStorageLocation { get; set; }

    [Required]
    public required BlobStorageLocation AnonymizedBlobStorageLocation { get; set; }
}
