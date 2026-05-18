using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#pragma warning disable CS8618
namespace api.Database.Models;

public class Analysis
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    public required string Name { get; set; }

    private DateTime _createdAt = DateTime.UtcNow;

    [Required]
    public DateTime CreatedAt
    {
        get => _createdAt;
        set => _createdAt = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }

    public Guid? AnalysisGroupId { get; set; }

    [ForeignKey(nameof(AnalysisGroupId))]
    public AnalysisGroup? AnalysisGroup { get; set; }

    public List<InspectionRecord> InspectionRecords { get; set; } = [];

    public List<AnalysisRun> Runs { get; set; } = [];
}
