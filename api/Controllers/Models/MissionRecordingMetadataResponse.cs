namespace api.Controllers.Models;

public class MissionRecordingMetadataResponse
{
    public string? RunId { get; set; }

    public string? IsarId { get; set; }

    public string? MissionName { get; set; }

    public required string Status { get; set; }

    public string? StartedAt { get; set; }

    public string? EndedAt { get; set; }

    public List<RecordingGapResponse> Gaps { get; set; } = [];

    public List<RecordingPoseSampleResponse> PoseSamples { get; set; } = [];

    public Dictionary<string, CameraRecordingMetadataResponse> Cameras { get; set; } = [];
}

public class CameraRecordingMetadataResponse
{
    public required string CameraId { get; set; }

    public required string Key { get; set; }

    public required string Status { get; set; }

    public string? FirstFrameAt { get; set; }

    public string? Error { get; set; }

    public List<SegmentTimingResponse> SegmentTimings { get; set; } = [];

    public string? InspectionId { get; set; }

    public string? VisualizationWorkflowType { get; set; }

    public string? VisualizationStatus { get; set; }

    public bool? PreservesFrameTiming { get; set; }
}

public class SegmentTimingResponse
{
    public int SegmentIndex { get; set; }

    public double VideoStartSeconds { get; set; }

    public double DurationSeconds { get; set; }

    public string? CaptureStartAt { get; set; }
}

public class RecordingPoseSampleResponse
{
    public required string ReceivedAt { get; set; }

    public string? SourceTimestamp { get; set; }

    public required RecordingPoseResponse Pose { get; set; }

    public string? MapName { get; set; }

    public double? Confidence { get; set; }
}

public class RecordingPoseResponse
{
    public required RecordingPositionResponse Position { get; set; }

    public required RecordingOrientationResponse Orientation { get; set; }

    public string Frame { get; set; } = "asset";
}

public class RecordingPositionResponse
{
    public double X { get; set; }

    public double Y { get; set; }

    public double Z { get; set; }
}

public class RecordingOrientationResponse
{
    public double X { get; set; }

    public double Y { get; set; }

    public double Z { get; set; }

    public double W { get; set; }
}
