using api.Database.Context;
using api.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

public class ThermalReferenceMetadataInput
{
    public required string TagId { get; set; }

    public required string InstallationCode { get; set; }

    public required string InspectionDescription { get; set; }

    public required BlobStorageLocation ReferenceBlobStorageDirectoryLocation { get; set; }
}

public interface IThermalReferenceMetadataService
{
    public Task<List<ThermalReferenceMetadata>> GetThermalReferenceMetadatas();

    public Task<ThermalReferenceMetadata?> ReadById(Guid id);

    public Task<ThermalReferenceMetadata?> ReadByUniqueKey(
        string installationCode,
        string tagId,
        string inspectionDescription
    );

    public Task<ThermalReferenceMetadata> CreateThermalReferenceMetadata(
        ThermalReferenceMetadataInput input,
        BlobStorageLocation referenceImageLocation,
        BlobStorageLocation referencePolygonLocation
    );

    public Task<ThermalReferenceMetadata> UpdateThermalReferenceMetadata(
        Guid id,
        ThermalReferenceMetadataInput input,
        BlobStorageLocation referenceImageLocation,
        BlobStorageLocation referencePolygonLocation
    );

    public Task RemoveThermalReferenceMetadata(Guid id);
}

public class ThermalReferenceMetadataService(
    SaraDbContext context,
    ILogger<ThermalReferenceMetadataService> logger
) : IThermalReferenceMetadataService
{
    private readonly ILogger<ThermalReferenceMetadataService> _logger = logger;

    public async Task<List<ThermalReferenceMetadata>> GetThermalReferenceMetadatas()
    {
        return await context
            .ThermalReferenceMetadata.OrderByDescending(reference => reference.DateCreated)
            .ThenBy(reference => reference.TagId)
            .ToListAsync();
    }

    public async Task<ThermalReferenceMetadata?> ReadById(Guid id)
    {
        return await context.ThermalReferenceMetadata.FirstOrDefaultAsync(reference =>
            reference.Id == id
        );
    }

    public async Task<ThermalReferenceMetadata?> ReadByUniqueKey(
        string installationCode,
        string tagId,
        string inspectionDescription
    )
    {
        return await context.ThermalReferenceMetadata.FirstOrDefaultAsync(reference =>
            reference.InstallationCode.ToLower().Equals(installationCode.ToLower())
            && reference.TagId.ToLower().Equals(tagId.ToLower())
            && reference.InspectionDescription.ToLower().Equals(inspectionDescription.ToLower())
        );
    }

    public async Task<ThermalReferenceMetadata> CreateThermalReferenceMetadata(
        ThermalReferenceMetadataInput input,
        BlobStorageLocation referenceImageLocation,
        BlobStorageLocation referencePolygonLocation
    )
    {
        await ThrowIfDuplicateExists(input, null);

        var thermalReferenceMetadata = new ThermalReferenceMetadata
        {
            TagId = input.TagId,
            InstallationCode = input.InstallationCode,
            InspectionDescription = input.InspectionDescription,
            ReferenceImageBlobStorageLocation = referenceImageLocation,
            ReferencePolygonBlobStorageLocation = referencePolygonLocation,
        };

        context.ThermalReferenceMetadata.Add(thermalReferenceMetadata);
        await context.SaveChangesAsync();
        return thermalReferenceMetadata;
    }

    public async Task<ThermalReferenceMetadata> UpdateThermalReferenceMetadata(
        Guid id,
        ThermalReferenceMetadataInput input,
        BlobStorageLocation referenceImageLocation,
        BlobStorageLocation referencePolygonLocation
    )
    {
        var thermalReferenceMetadata =
            await ReadById(id)
            ?? throw new KeyNotFoundException(
                $"Thermal reference metadata with id {id} was not found"
            );

        await ThrowIfDuplicateExists(input, id);

        thermalReferenceMetadata.TagId = input.TagId;
        thermalReferenceMetadata.InstallationCode = input.InstallationCode;
        thermalReferenceMetadata.InspectionDescription = input.InspectionDescription;
        thermalReferenceMetadata.ReferenceImageBlobStorageLocation = referenceImageLocation;
        thermalReferenceMetadata.ReferencePolygonBlobStorageLocation = referencePolygonLocation;

        context.ThermalReferenceMetadata.Update(thermalReferenceMetadata);
        await context.SaveChangesAsync();
        return thermalReferenceMetadata;
    }

    public async Task RemoveThermalReferenceMetadata(Guid id)
    {
        var thermalReferenceMetadata =
            await ReadById(id)
            ?? throw new KeyNotFoundException(
                $"Thermal reference metadata with id {id} was not found"
            );

        context.ThermalReferenceMetadata.Remove(thermalReferenceMetadata);
        await context.SaveChangesAsync();
    }

    private async Task ThrowIfDuplicateExists(ThermalReferenceMetadataInput input, Guid? existingId)
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
            input.InstallationCode,
            input.TagId,
            input.InspectionDescription
        );
        throw new ArgumentException(
            "A thermal reference metadata already exists for this installation code, tag ID, and inspection description"
        );
    }
}
