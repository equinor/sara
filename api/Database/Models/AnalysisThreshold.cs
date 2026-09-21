using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace api.Database.Models;

public enum ThresholdSource
{
    Config,

    Manual,

    MissionDefinition,
}

public class AnalysisThreshold
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    public Guid AnalysisId { get; set; }

    [ForeignKey(nameof(AnalysisId))]
    public Analysis? Analysis { get; set; }

    [Required]
    public required string Key { get; set; }

    public double? LowerAlert { get; set; }

    public double? LowerWarning { get; set; }

    public double? UpperWarning { get; set; }

    public double? UpperAlert { get; set; }

    public bool? AlertWhenTrue { get; set; }

    public double? MinConfidence { get; set; }

    [Required]
    public ThresholdSource Source { get; set; } = ThresholdSource.Config;
}
