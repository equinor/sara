#pragma warning disable CS8618
using api.Database.Models;

namespace api.Services.Models;

public class StidUploadMessage
{
    public string InspectionId { get; set; }

    public BlobStorageLocation AnonymizedBlobStorageLocation { get; set; }
}
