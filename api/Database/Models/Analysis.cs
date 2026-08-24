using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace api.Database.Models;

public enum AnalysisTypeEnum
{
    Fencilla,
    CLOE,
    ThermalReading,
    CO2,
}

public class Analysis
{
    public static string GetAnalysisTypeFromAnalysisEnum(AnalysisTypeEnum type)
    {
        var analysisEnumToAnalysisStringMapping = new Dictionary<AnalysisTypeEnum, string>
        {
            { AnalysisTypeEnum.CLOE, "cloe" },
            { AnalysisTypeEnum.Fencilla, "fencilla" },
            { AnalysisTypeEnum.ThermalReading, "thermal-reading" },
            { AnalysisTypeEnum.CO2, "CO2" },
        };
        return analysisEnumToAnalysisStringMapping[type];
    }

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    public required string AnalysisType { get; set; }

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
