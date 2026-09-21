using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using api.Database.Models;

namespace api.Controllers.Models;

public class AnalysisResultValueDto
{
    // Without this, System.Text.Json treats the mapping constructor below as the
    // deserialization constructor and fails to bind, so clients cannot read the DTO back.
    [JsonConstructor]
#nullable disable
    public AnalysisResultValueDto() { }

#nullable enable

    [SetsRequiredMembers]
    public AnalysisResultValueDto(AnalysisResultValue value)
    {
        Id = value.Id;
        AnalysisRunId = value.AnalysisRunId;
        SourceWorkflowId = value.SourceWorkflowId;
        AnalysisType = value.AnalysisType;
        InstallationCode = value.InstallationCode;
        Tag = value.Tag;
        InspectionDescription = value.InspectionDescription;
        Key = value.Key;
        ValueKind = value.ValueKind;
        NumericValue = value.NumericValue;
        BooleanValue = value.BooleanValue;
        TextValue = value.TextValue;
        Unit = value.Unit;
        Confidence = value.Confidence;
        ModelMessage = value.ModelMessage;
        Severity = value.Severity;
        Acknowledged = value.Acknowledged;
        MeasuredAt = value.MeasuredAt;
    }

    public Guid Id { get; set; }
    public Guid AnalysisRunId { get; set; }
    public Guid? SourceWorkflowId { get; set; }
    public required string AnalysisType { get; set; }
    public required string InstallationCode { get; set; }
    public string? Tag { get; set; }
    public string? InspectionDescription { get; set; }
    public required string Key { get; set; }

    public ResultValueKind ValueKind { get; set; }
    public double? NumericValue { get; set; }
    public bool? BooleanValue { get; set; }
    public string? TextValue { get; set; }
    public string? Unit { get; set; }

    public double? Confidence { get; set; }
    public string? ModelMessage { get; set; }
    public ResultSeverity Severity { get; set; }
    public bool Acknowledged { get; set; }
    public DateTime MeasuredAt { get; set; }
}
