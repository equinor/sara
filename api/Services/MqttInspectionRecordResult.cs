using api.Database.Models;

namespace api.Services;

public record MqttInspectionRecordResult(InspectionRecord Record, bool IsDuplicate);
