using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace api.Database.Models;

public class AnalysisRunFeedback
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    public Guid AnalysisRunId { get; set; }

    [ForeignKey(nameof(AnalysisRunId))]
    public required AnalysisRun AnalysisRun { get; set; }

    [Required]
    public bool IsCorrect { get; set; }
}
