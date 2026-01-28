using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#pragma warning disable CS8618
namespace api.Database.Models;

public enum AnalysisType
{
    ConstantLevelOiler,
    Fencilla,
    ThermalReading,
}

public class AnalysisMapping(
    string tag,
    string inspectionDescription,
    List<AnalysisType> analysesToBeRun
)
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    public string Tag { get; set; } = tag;

    [Required]
    public string InspectionDescription { get; set; } = inspectionDescription;

    [Required]
    public List<AnalysisType> AnalysesToBeRun { get; set; } = analysesToBeRun;
}
