using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#pragma warning disable CS8618
namespace api.Database.Models;

public class Metadata(string? tag, InspectionType? type, string? inspectionDescription)
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }

    public string? Tag { get; set; } = tag;

    public InspectionType? Type { get; set; } = type;

    public string? InspectionDescription { get; set; } = inspectionDescription;

    public static InspectionType? TypeFromString(string? status)
    {
        if (string.IsNullOrEmpty(status))
            return null;
        status = status.ToLowerInvariant();
        return status switch
        {
            "image" => InspectionType.Image,
            "thermalimage" => InspectionType.ThermalImage,
            _ => null,
        };
    }

    public static string? TypeToString(InspectionType type)
    {
        return type switch
        {
            InspectionType.Image => "image",
            InspectionType.ThermalImage => "thermalimage",
            _ => null,
        };
    }
}

public enum InspectionType
{
    Image,
    ThermalImage,
}
