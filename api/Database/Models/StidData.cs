#pragma warning disable CS8618
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Policy;

namespace api.Database.Models;

public class StidData
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    public string InspectionId { get; set; }
    public BlobStorageLocation AnonymizedBlobStorageLocation { get; set; }
    public string Tag { get; set; }
    public string Description { get; set; }
    public WorkflowStatus StidWorkflowStatus { get; set; } = WorkflowStatus.NotStarted;
    public int? StidMediaId { get; set; }
}
