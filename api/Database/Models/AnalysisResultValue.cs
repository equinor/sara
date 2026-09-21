using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace api.Database.Models;

public enum ResultValueKind
{
    Numeric,
    Boolean,
    Text,
}

public enum ResultSeverity
{
    NotEvaluated,
    Inconclusive,
    Ok,
    Warning,
    Alert,
}

public class AnalysisResultValue
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    public Guid AnalysisRunId { get; set; }

    [ForeignKey(nameof(AnalysisRunId))]
    public AnalysisRun? AnalysisRun { get; set; }

    public Guid? SourceWorkflowId { get; set; }

    [Required]
    public required string AnalysisType { get; set; }

    [Required]
    public required string InstallationCode { get; set; }

    public string? Tag { get; set; }

    public string? InspectionDescription { get; set; }

    /// <summary>
    /// Composed at write time so the "latest value per correlation key" query is a
    /// single indexed comparison rather than null-safe equality across components.
    /// </summary>
    [Required]
    public required string CorrelationKey { get; set; }

    [Required]
    public required string Key { get; set; }

    [Required]
    public ResultValueKind ValueKind { get; set; }

    public double? NumericValue { get; set; }

    public bool? BooleanValue { get; set; }

    public string? TextValue { get; set; }

    public string? Unit { get; set; }

    public double? Confidence { get; set; }

    public string? ModelMessage { get; set; }

    [Required]
    public ResultSeverity Severity { get; set; } = ResultSeverity.NotEvaluated;

    public string? ThresholdSnapshotJson { get; set; }

    public bool Acknowledged { get; set; }

    private DateTime _measuredAt = DateTime.UtcNow;

    /// <summary>
    /// When the robot took the reading. Ordering by CreatedAt instead would let a
    /// delayed re-analysis of an older image clear an alarm from a newer reading.
    /// </summary>
    [Required]
    public DateTime MeasuredAt
    {
        get => _measuredAt;
        set => _measuredAt = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }

    private DateTime _createdAt = DateTime.UtcNow;

    [Required]
    public DateTime CreatedAt
    {
        get => _createdAt;
        set => _createdAt = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }

    public static string BuildCorrelationKey(
        string installationCode,
        string? tag,
        string? inspectionDescription,
        string analysisType,
        string key
    ) =>
        string.Join(
            '|',
            installationCode,
            tag ?? "",
            inspectionDescription ?? "",
            analysisType,
            key
        );
}
