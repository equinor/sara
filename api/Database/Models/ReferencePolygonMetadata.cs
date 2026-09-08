using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

#pragma warning disable CS8618
namespace api.Database.Models;

public class ImageCoordinate
{
    [Required]
    public required double X { get; set; }

    [Required]
    public required double Y { get; set; }

    public override string ToString()
    {
        return "(" + X + ", " + Y + ")";
    }
}

public class ReferencePolygonMetadata
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    public required string TagId { get; set; }

    [Required]
    public required string InstallationCode { get; set; }

    [Required]
    public required string InspectionDescription { get; set; }

    private DateTime _dateCreated = DateTime.UtcNow;

    [Required]
    public DateTime DateCreated
    {
        get => _dateCreated;
        set => _dateCreated = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }

    [Required]
    public required BlobStorageLocation ReferenceImageBlobStorageLocation { get; set; }

    [Required]
    public required List<ImageCoordinate> Polygon { get; set; }

    [Required]
    public required AnalysisTypeEnum SourceAnalysisType { get; set; }

    public static string GetReferenceImageFileExtension(AnalysisTypeEnum sourceAnalysisType) =>
        sourceAnalysisType switch
        {
            AnalysisTypeEnum.ThermalReading => "tiff",
            AnalysisTypeEnum.Fencilla => "jpg",
            _ => throw new NotSupportedException(
                $"No reference image file extension configured for analysis type '{sourceAnalysisType}'"
            ),
        };
}
