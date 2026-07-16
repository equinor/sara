using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace api.Database.Models;

public enum AnalysisType
{
    Fencilla,
    CLOE,
    ThermalReading,
    CO2,
}

public class Analysis
{
    public static string GetWorkflowTypeFromAnalysisType(AnalysisType type)
    {
        var analysisToWorkflowTypeMapping = new Dictionary<AnalysisType, string>
        {
            { AnalysisType.CLOE, "cloe" },
            { AnalysisType.Fencilla, "fencilla" },
            { AnalysisType.ThermalReading, "thermal-reading" },
            { AnalysisType.CO2, "CO2" },
        };
        return analysisToWorkflowTypeMapping[type];
    }

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
