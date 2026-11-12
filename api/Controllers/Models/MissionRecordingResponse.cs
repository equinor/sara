namespace api.Controllers.Models;

public static class RecordingStatus
{
    public const string NotRecorded = "NotRecorded";
    public const string Processing = "Processing";
    public const string Ready = "Ready";
    public const string Failed = "Failed";
}

/// <summary>A mission's recording session, in the shape Flotilla's recording UI reads.</summary>
public class MissionRecordingResponse
{
    public required string Status { get; set; }

    public List<RecordingGapResponse> Gaps { get; set; } = [];

    public Dictionary<string, CameraRecordingResponse> Cameras { get; set; } = [];
}

public class CameraRecordingResponse
{
    public required string CameraId { get; set; }

    public required string Key { get; set; }

    public required string Status { get; set; }

    public string? FirstFrameAt { get; set; }

    public string? Error { get; set; }
}

public class RecordingGapResponse
{
    public required string At { get; set; }

    public required string Reason { get; set; }
}

public class RecordingAccessResponse
{
    public required Uri PlaybackUrl { get; set; }

    public required Uri DownloadUrl { get; set; }

    public required DateTimeOffset ExpiresAt { get; set; }
}
