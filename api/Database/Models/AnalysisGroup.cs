using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace api.Database.Models;

#pragma warning disable CS8618

public enum AnalysisGroupStatus
{
    Pending,
    Complete,
    TimedOut,
}

public class AnalysisGroup
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    public required string GroupId { get; set; }

    [Required]
    public int ExpectedSize { get; set; }

    public AnalysisGroupStatus Status { get; set; } = AnalysisGroupStatus.Pending;

    public DateTime? TimeoutAt { get; set; }

    public List<InspectionRecord> InspectionRecords { get; set; } = [];

    public List<Analysis> Analyses { get; set; } = [];
}
