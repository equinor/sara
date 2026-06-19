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

public class ThermalReferenceMetadataInput
{
    public required string TagId { get; set; }

    public required string InstallationCode { get; set; }

    public required string InspectionDescription { get; set; }

    public required BlobDirectoryInput ReferenceBlobStorageDirectory { get; set; }
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

    public Task<ThermalReferenceMetadata> CreateFromInspectionRecord(
        InspectionRecord record,
        string tagId,
        string installationCode,
        string inspectionDescription,
        double[][] polygon
    );
}

public class ThermalReferenceMetadataService(
    SaraDbContext context,
    ILogger<ThermalReferenceMetadataService> logger,
    IBlobStorageService blobStorageService,
    IConfiguration configuration
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

    public async Task<ThermalReferenceMetadata> CreateFromInspectionRecord(
        InspectionRecord record,
        string tagId,
        string installationCode,
        string inspectionDescription,
        double[][] polygon
    )
    {
        var preprocessedLocation = GetPreprocessedBlobLocation(record);
        var (imageDestination, polygonDestination) = BuildReferenceLocations(
            tagId,
            inspectionDescription,
            preprocessedLocation.BlobContainer
        );

        var input = new ThermalReferenceMetadataInput
        {
            TagId = tagId,
            InstallationCode = installationCode,
            InspectionDescription = inspectionDescription,
            ReferenceBlobStorageDirectory = new BlobDirectoryInput
            {
                BlobContainer = imageDestination.BlobContainer,
                BlobName = $"{tagId}_{inspectionDescription}",
            },
        };

        await ThrowIfDuplicateExists(input, null);

        await blobStorageService.CopyBlobAsync(preprocessedLocation, imageDestination);
        await UploadPolygonAsync(polygon, polygonDestination);

        return await CreateThermalReferenceMetadata(input, imageDestination, polygonDestination);
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

    private (
        BlobStorageLocation imageLocation,
        BlobStorageLocation polygonLocation
    ) BuildReferenceLocations(string tagId, string inspectionDescription, string blobContainer)
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

        var polygonLocation = new BlobStorageLocation
        {
            StorageAccount = storageAccount,
            BlobContainer = blobContainer,
            BlobName = $"{directory}/reference_polygon.json",
        };

        return (imageLocation, polygonLocation);
    }

    private async Task UploadPolygonAsync(double[][] polygon, BlobStorageLocation destination)
    {
        var polygonJson = JsonSerializer.Serialize(polygon);
        using var polygonStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(polygonJson));
        await blobStorageService.UploadBlobAsync(destination, polygonStream, "application/json");
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
            Sanitize.SanitizeUserInput(input.InstallationCode),
            Sanitize.SanitizeUserInput(input.TagId),
            Sanitize.SanitizeUserInput(input.InspectionDescription)
        );
        throw new ArgumentException(
            "A thermal reference metadata already exists for this installation code, tag ID, and inspection description"
        );
    }
}
