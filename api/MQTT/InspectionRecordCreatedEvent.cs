namespace api.MQTT;

public class InspectionRecordCreatedEvent
{
    public required Guid InspectionRecordId { get; set; }

    public List<string>? RequiredAnalysis { get; set; }

    public IsarAnalysisGroupMessage? AnalysisGroup { get; set; }
}
