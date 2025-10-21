using System.ComponentModel.DataAnnotations;

namespace api.Options;

public sealed class StorageOptions
{
    [Required]
    public string RawStorageAccount { get; init; } = default!;

    [Required]
    public string AnonStorageAccount { get; init; } = default!;

    [Required]
    public string VisStorageAccount { get; init; } = default!;
}
