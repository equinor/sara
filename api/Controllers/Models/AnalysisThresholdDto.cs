using api.Database.Models;

namespace api.Controllers.Models;

public class AnalysisThresholdDto
{
    public AnalysisThresholdDto() { }

    public AnalysisThresholdDto(AnalysisThreshold threshold)
    {
        Id = threshold.Id;
        Key = threshold.Key;
        LowerAlert = threshold.LowerAlert;
        LowerWarning = threshold.LowerWarning;
        UpperWarning = threshold.UpperWarning;
        UpperAlert = threshold.UpperAlert;
        AlertWhenTrue = threshold.AlertWhenTrue;
        MinConfidence = threshold.MinConfidence;
        Source = threshold.Source;
    }

    public Guid Id { get; set; }
    public string Key { get; set; } = "";
    public double? LowerAlert { get; set; }
    public double? LowerWarning { get; set; }
    public double? UpperWarning { get; set; }
    public double? UpperAlert { get; set; }
    public bool? AlertWhenTrue { get; set; }
    public double? MinConfidence { get; set; }
    public ThresholdSource Source { get; set; }
}
