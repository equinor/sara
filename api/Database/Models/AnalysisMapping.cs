using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace api.Database.Models;

public class AnalysisMapping(string tag, string inspectionDescription)
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    public string Tag { get; set; } = tag;

    [Required]
    public string InspectionDescription { get; set; } = inspectionDescription;

    [Required]
    public List<AnalysisType> AnalysesToBeRun { get; set; } = [];
}
