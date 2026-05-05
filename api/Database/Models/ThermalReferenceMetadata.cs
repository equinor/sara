using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#pragma warning disable CS8618
namespace api.Database.Models;

public class ThermalReferenceMetadata
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
    public required BlobStorageLocation ReferencePolygonBlobStorageLocation { get; set; }
}
