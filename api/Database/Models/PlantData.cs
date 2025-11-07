using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace api.Database.Models;


[Owned]
public class BlobStorageLocation
{
    [Required]
    public required string StorageAccount { get; set; }

    [Required]
    public required string BlobContainer { get; set; }

    [Required]
    public required string BlobName { get; set; }
}

public class PlantData
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    [Required]
    public required string InspectionId { get; set; }

    [Required]
    public required string InstallationCode { get; set; }

    [Required]
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    public Metadata? Metadata { get; set; }

    [Required]
    public required Anonymization Anonymization { get; set; }

    public CLOEAnalysis? CLOEAnalysis { get; set; }

    public FencillaAnalysis? FencillaAnalysis { get; set; }
}
