using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#pragma warning disable CS8618
namespace api.Database.Models;

public class InspectionRecord
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    public required string InspectionId { get; set; }

    [Required]
    public required string InstallationCode { get; set; }

    [Required]
    public required BlobStorageLocation BlobStorageLocation { get; set; }

    private DateTime _createdAt = DateTime.UtcNow;

    [Required]
    public DateTime CreatedAt
    {
        get => _createdAt;
        set => _createdAt = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }

    public string? InspectionType { get; set; }

    public string? Tag { get; set; }

    public Position? TargetPosition { get; set; }

    public Pose? RobotPose { get; set; }

    public string? InspectionDescription { get; set; }

    public string? RobotName { get; set; }

    private DateTime? _timestamp;
    public DateTime? Timestamp
    {
        get => _timestamp;
        set => _timestamp = value?.Kind == DateTimeKind.Utc ? value : value?.ToUniversalTime();
    }

    public Guid? AnalysisGroupId { get; set; }

    [ForeignKey(nameof(AnalysisGroupId))]
    public AnalysisGroup? AnalysisGroup { get; set; }

    public List<Analysis> Analyses { get; set; } = [];
}
