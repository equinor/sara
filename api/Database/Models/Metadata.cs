using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#pragma warning disable CS8618
namespace api.Database.Models;

public class Metadata(string tag, string coordinates)
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }

    public string? Tag { get; set; } = tag;

    public string? Coordinates { get; set; } = coordinates;
}
