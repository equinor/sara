using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

#pragma warning disable CS8618
namespace api.Database.Models;

public class PlantData
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }

    [Required]
    public required string InspectionId { get; set; }

    [Required]
    public required string InstallationCode { get; set; }

    private DateTime _dateCreated = DateTime.UtcNow;

    [Required]
    public DateTime DateCreated
    {
        get => _dateCreated;
        set => _dateCreated = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }

    public string? Tag { get; set; }

    public string? Coordinates { get; set; }

    public string? InspectionDescription { get; set; }

    private DateTime? _timestamp;
    public DateTime? Timestamp
    {
        get => _timestamp;
        set => _timestamp = value?.Kind == DateTimeKind.Utc ? value : value?.ToUniversalTime();
    }

    [Required]
    public required Anonymization Anonymization { get; set; }

    public CLOEAnalysis? CLOEAnalysis { get; set; }

    public FencillaAnalysis? FencillaAnalysis { get; set; }
}
