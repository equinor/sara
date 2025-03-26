using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#pragma warning disable CS8618
namespace api.Database.Models;

public class TagAnalysis
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }

    [Required]
    public string Tag { get; set; }

    [Required]
    public List<AnalysisType> AnalysisToBeRun { get; set; } = [];
}
