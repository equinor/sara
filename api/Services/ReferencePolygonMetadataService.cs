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

    public required AnalysisTypeEnum SourceAnalysisType { get; set; }
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
        List<ImageCoordinate> polygon,
        AnalysisTypeEnum sourceAnalysisType
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

    private static readonly AnalysisTypeEnum[] SupportedSourceAnalysisTypes =
    [
        AnalysisTypeEnum.ThermalReading,
        AnalysisTypeEnum.Fencilla,
    ];

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
            SourceAnalysisType = input.SourceAnalysisType,
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
                $"Reference polygon metadata with id {id} was not found"
            );

        await ThrowIfDuplicateExists(input, id);

        referencePolygonMetadata.TagId = input.TagId;
        referencePolygonMetadata.InstallationCode = input.InstallationCode;
        referencePolygonMetadata.InspectionDescription = input.InspectionDescription;
        referencePolygonMetadata.ReferenceImageBlobStorageLocation = referenceImageLocation;
        referencePolygonMetadata.Polygon = input.Polygon;
        referencePolygonMetadata.SourceAnalysisType = input.SourceAnalysisType;

        context.ReferencePolygonMetadata.Update(referencePolygonMetadata);
        await context.SaveChangesAsync();
        return referencePolygonMetadata;
    }

    public async Task RemoveReferencePolygonMetadata(Guid id)
    {
        var referencePolygonMetadata =
            await ReadById(id)
            ?? throw new KeyNotFoundException(
                $"Reference polygon metadata with id {id} was not found"
            );

        context.ReferencePolygonMetadata.Remove(referencePolygonMetadata);
        await context.SaveChangesAsync();
    }

    public async Task<ReferencePolygonMetadata> CreateFromInspectionRecord(
        InspectionRecord record,
        string tagId,
        string installationCode,
        string inspectionDescription,
        List<ImageCoordinate> polygon,
        AnalysisTypeEnum sourceAnalysisType
    )
    {
        if (!SupportedSourceAnalysisTypes.Contains(sourceAnalysisType))
        {
            throw new NotSupportedException(
                $"Unsupported source analysis type '{sourceAnalysisType}' for creating a reference polygon"
            );
        }

        var preprocessedLocation = ResolveSourceBlobLocation(record, sourceAnalysisType);
        var imageDestination = BuildReferenceLocation(
            tagId,
            inspectionDescription,
            preprocessedLocation.BlobContainer,
            sourceAnalysisType
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
            SourceAnalysisType = sourceAnalysisType,
        };

        await ThrowIfDuplicateExists(input, null);

        await blobStorageService.CopyBlobAsync(preprocessedLocation, imageDestination);

        return await CreateReferencePolygonMetadata(input, imageDestination);
    }

    private static BlobStorageLocation ResolveSourceBlobLocation(
        InspectionRecord record,
        AnalysisTypeEnum sourceAnalysisType
    )
    {
        var workflowType = Analysis.GetAnalysisTypeFromAnalysisEnum(sourceAnalysisType);
        var sourceWorkflow =
            record.FindLatestWorkflow(sourceAnalysisType)
            ?? throw new KeyNotFoundException(
                $"No {workflowType} workflow found for inspection record {record.Id}"
            );

        if (
            sourceWorkflow.InputBlobStorageLocations is null
            || sourceWorkflow.InputBlobStorageLocations.Count == 0
        )
        {
            throw new KeyNotFoundException($"{workflowType} workflow has no input blob location");
        }

        return sourceWorkflow.InputBlobStorageLocations[0];
    }

    private BlobStorageLocation BuildReferenceLocation(
        string tagId,
        string inspectionDescription,
        string blobContainer,
        AnalysisTypeEnum sourceAnalysisType
    )
    {
        var storageAccount =
            configuration["Storage:ThermalReferenceStorageAccount"]
            ?? throw new InvalidOperationException(
                "Storage:ThermalReferenceStorageAccount is not configured"
            );

        var directory = $"{tagId}_{inspectionDescription}";
        var extension = ReferencePolygonMetadata.GetReferenceImageFileExtension(sourceAnalysisType);

        var imageLocation = new BlobStorageLocation
        {
            StorageAccount = storageAccount,
            BlobContainer = blobContainer,
            BlobName = $"{directory}/reference_image.{extension}",
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
            "Reference polygon metadata already exists for InstallationCode {InstallationCode}, TagId {TagId}, InspectionDescription {InspectionDescription}",
            Sanitize.SanitizeUserInput(input.InstallationCode),
            Sanitize.SanitizeUserInput(input.TagId),
            Sanitize.SanitizeUserInput(input.InspectionDescription)
        );
        throw new ArgumentException(
            "A reference polygon metadata already exists for this installation code, tag ID, and inspection description"
        );
    }
}
