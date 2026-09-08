using System.Text.Json;
using api.Database.Context;
using api.Database.Models;
using api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

public class BlobDirectoryInput
{
    public required string BlobContainer { get; set; }

    public required string BlobName { get; set; }
}

public class ReferencePolygonMetadataInput
{
    public required string TagId { get; set; }

    public required string InstallationCode { get; set; }

    public required string InspectionDescription { get; set; }

    public required BlobDirectoryInput ReferenceBlobStorageDirectory { get; set; }

    public required List<ImageCoordinate> Polygon { get; set; }
}

public interface IReferencePolygonMetadataService
{
    public Task<List<ReferencePolygonMetadata>> GetReferencePolygonMetadatas();

    public Task<ReferencePolygonMetadata?> ReadById(Guid id);

    public Task<ReferencePolygonMetadata?> ReadByUniqueKey(
        string installationCode,
        string tagId,
        string inspectionDescription
    );

    public Task<ReferencePolygonMetadata> CreateReferencePolygonMetadata(
        ReferencePolygonMetadataInput input,
        BlobStorageLocation referenceImageLocation
    );

    public Task<ReferencePolygonMetadata> UpdateReferencePolygonMetadata(
        Guid id,
        ReferencePolygonMetadataInput input,
        BlobStorageLocation referenceImageLocation
    );

    public Task RemoveReferencePolygonMetadata(Guid id);

    public Task<ReferencePolygonMetadata> CreateFromInspectionRecord(
        InspectionRecord record,
        string tagId,
        string installationCode,
        string inspectionDescription,
        List<ImageCoordinate> polygon
    );
}

public class ReferencePolygonMetadataService(
    SaraDbContext context,
    ILogger<ReferencePolygonMetadataService> logger,
    IBlobStorageService blobStorageService,
    IConfiguration configuration
) : IReferencePolygonMetadataService
{
    private readonly ILogger<ReferencePolygonMetadataService> _logger = logger;

    public async Task<List<ReferencePolygonMetadata>> GetReferencePolygonMetadatas()
    {
        return await context
            .ReferencePolygonMetadata.OrderByDescending(reference => reference.DateCreated)
            .ThenBy(reference => reference.TagId)
            .ToListAsync();
    }

    public async Task<ReferencePolygonMetadata?> ReadById(Guid id)
    {
        return await context.ReferencePolygonMetadata.FirstOrDefaultAsync(reference =>
            reference.Id == id
        );
    }

    public async Task<ReferencePolygonMetadata?> ReadByUniqueKey(
        string installationCode,
        string tagId,
        string inspectionDescription
    )
    {
        return await context.ReferencePolygonMetadata.FirstOrDefaultAsync(reference =>
            reference.InstallationCode.ToLower().Equals(installationCode.ToLower())
            && reference.TagId.ToLower().Equals(tagId.ToLower())
            && reference.InspectionDescription.ToLower().Equals(inspectionDescription.ToLower())
        );
    }

    public async Task<ReferencePolygonMetadata> CreateReferencePolygonMetadata(
        ReferencePolygonMetadataInput input,
        BlobStorageLocation referenceImageLocation
    )
    {
        await ThrowIfDuplicateExists(input, null);

        var referencePolygonMetadata = new ReferencePolygonMetadata
        {
            TagId = input.TagId,
            InstallationCode = input.InstallationCode,
            InspectionDescription = input.InspectionDescription,
            ReferenceImageBlobStorageLocation = referenceImageLocation,
            Polygon = input.Polygon,
        };

        context.ReferencePolygonMetadata.Add(referencePolygonMetadata);
        await context.SaveChangesAsync();
        return referencePolygonMetadata;
    }

    public async Task<ReferencePolygonMetadata> UpdateReferencePolygonMetadata(
        Guid id,
        ReferencePolygonMetadataInput input,
        BlobStorageLocation referenceImageLocation
    )
    {
        var referencePolygonMetadata =
            await ReadById(id)
            ?? throw new KeyNotFoundException(
                $"Thermal reference metadata with id {id} was not found"
            );

        await ThrowIfDuplicateExists(input, id);

        referencePolygonMetadata.TagId = input.TagId;
        referencePolygonMetadata.InstallationCode = input.InstallationCode;
        referencePolygonMetadata.InspectionDescription = input.InspectionDescription;
        referencePolygonMetadata.ReferenceImageBlobStorageLocation = referenceImageLocation;
        referencePolygonMetadata.Polygon = input.Polygon;

        context.ReferencePolygonMetadata.Update(referencePolygonMetadata);
        await context.SaveChangesAsync();
        return referencePolygonMetadata;
    }

    public async Task RemoveReferencePolygonMetadata(Guid id)
    {
        var referencePolygonMetadata =
            await ReadById(id)
            ?? throw new KeyNotFoundException(
                $"Thermal reference metadata with id {id} was not found"
            );

        context.ReferencePolygonMetadata.Remove(referencePolygonMetadata);
        await context.SaveChangesAsync();
    }

    public async Task<ReferencePolygonMetadata> CreateFromInspectionRecord(
        InspectionRecord record,
        string tagId,
        string installationCode,
        string inspectionDescription,
        List<ImageCoordinate> polygon
    )
    {
        var preprocessedLocation = GetPreprocessedBlobLocation(record);
        var imageDestination = BuildReferenceLocation(
            tagId,
            inspectionDescription,
            preprocessedLocation.BlobContainer
        );

        var input = new ReferencePolygonMetadataInput
        {
            TagId = tagId,
            InstallationCode = installationCode,
            InspectionDescription = inspectionDescription,
            ReferenceBlobStorageDirectory = new BlobDirectoryInput
            {
                BlobContainer = imageDestination.BlobContainer,
                BlobName = $"{tagId}_{inspectionDescription}",
            },
            Polygon = polygon,
        };

        await ThrowIfDuplicateExists(input, null);

        await blobStorageService.CopyBlobAsync(preprocessedLocation, imageDestination);

        return await CreateReferencePolygonMetadata(input, imageDestination);
    }

    private static BlobStorageLocation GetPreprocessedBlobLocation(InspectionRecord record)
    {
        var thermalReadingWorkflow =
            record
                .Analyses.SelectMany(a => a.Runs)
                .SelectMany(r => r.Workflows)
                .Where(w =>
                    w.WorkflowType.Equals("thermal-reading", StringComparison.OrdinalIgnoreCase)
                )
                .OrderByDescending(w => w.CompletedAt ?? w.StartedAt ?? DateTime.MinValue)
                .FirstOrDefault()
            ?? throw new KeyNotFoundException(
                $"No thermal-reading workflow found for inspection record {record.Id}"
            );

        if (
            thermalReadingWorkflow.InputBlobStorageLocations is null
            || thermalReadingWorkflow.InputBlobStorageLocations.Count == 0
        )
        {
            throw new KeyNotFoundException("Thermal-reading workflow has no input blob location");
        }

        return thermalReadingWorkflow.InputBlobStorageLocations[0];
    }

    private BlobStorageLocation BuildReferenceLocation(
        string tagId,
        string inspectionDescription,
        string blobContainer
    )
    {
        var storageAccount =
            configuration["Storage:ThermalReferenceStorageAccount"]
            ?? throw new InvalidOperationException(
                "Storage:ThermalReferenceStorageAccount is not configured"
            );

        var directory = $"{tagId}_{inspectionDescription}";

        var imageLocation = new BlobStorageLocation
        {
            StorageAccount = storageAccount,
            BlobContainer = blobContainer,
            BlobName = $"{directory}/reference_image.tiff",
        };

        return imageLocation;
    }

    private async Task ThrowIfDuplicateExists(ReferencePolygonMetadataInput input, Guid? existingId)
    {
        var existingReference = await ReadByUniqueKey(
            input.InstallationCode,
            input.TagId,
            input.InspectionDescription
        );

        if (existingReference is null || existingReference.Id == existingId)
        {
            return;
        }

        _logger.LogWarning(
            "Thermal reference metadata already exists for InstallationCode {InstallationCode}, TagId {TagId}, InspectionDescription {InspectionDescription}",
            Sanitize.SanitizeUserInput(input.InstallationCode),
            Sanitize.SanitizeUserInput(input.TagId),
            Sanitize.SanitizeUserInput(input.InspectionDescription)
        );
        throw new ArgumentException(
            "A thermal reference metadata already exists for this installation code, tag ID, and inspection description"
        );
    }
}
