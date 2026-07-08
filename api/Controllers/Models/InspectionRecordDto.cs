using api.Database.Models;
using api.Services;

namespace api.Controllers.Models;

public class InspectionRecordDto(InspectionRecord record, IBlobStorageService blobService)
{
    public Guid Id { get; set; } = record.Id;
    public string InspectionId { get; set; } = record.InspectionId;
    public string InstallationCode { get; set; } = record.InstallationCode;
    public BlobStorageLocation BlobStorageLocation { get; set; } = record.BlobStorageLocation;
    public DateTime CreatedAt { get; set; } = record.CreatedAt;
    public string? InspectionType { get; set; } = record.InspectionType;
    public string? Tag { get; set; } = record.Tag;
    public Position? TargetPosition { get; set; } = record.TargetPosition;
    public Pose? RobotPose { get; set; } = record.RobotPose;
    public List<AnalysisDto> Analyses { get; set; } =
    [.. record.Analyses.Select((a) => new AnalysisDto(a, blobService))];
    public string? InspectionDescription { get; set; } = record.InspectionDescription;
    public string? RobotName { get; set; } = record.RobotName;
    public DateTime? Timestamp { get; set; } = record.Timestamp;
    public Guid? AnalysisGroupId { get; set; } = record.AnalysisGroupId;
}
