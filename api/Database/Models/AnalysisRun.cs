using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#pragma warning disable CS8618
namespace api.Database.Models;

public enum AnalysisRunStatus
{
    Pending,
    InProgress,
    Succeeded,
    Failed,
}

public class AnalysisRun
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    public Guid AnalysisId { get; set; }

    [ForeignKey(nameof(AnalysisId))]
    public required Analysis Analysis { get; set; }

    [Required]
    public int RunNumber { get; set; }

    public AnalysisRunStatus Status { get; set; } = AnalysisRunStatus.Pending;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public List<Workflow> Workflows { get; set; } = [];
}
