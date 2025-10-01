using System.Text;
using System.Text.Json;
using api.Database.Context;
using api.Database.Models;
using api.MQTT;
using api.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

public class TriggerStidUploadWorkflowRequest(
    string inspectionId,
    BlobStorageLocation anonymizedBlobStorageLocation,
    string tagId,
    string description
)
{
    public string InspectionId { get; } = inspectionId;
    public BlobStorageLocation AnonymizedBlobStorageLocation { get; } =
        anonymizedBlobStorageLocation;
    public string TagId { get; } = tagId;
    public string Description { get; } = description;
}

public interface IStidWorkflowService
{
    public Task TriggerUploadToStid(StidUploadMessage data);
    public Task<StidData?> UpdateStidWorkflowStatus(string inspectionId, WorkflowStatus status);

    public Task UpdateStidMediaId(string inspectionId, int? mediaId);
}

public class StidWorkflowService(
    IConfiguration configuration,
    SaraDbContext context,
    ILogger<StidWorkflowService> logger,
    IPlantDataService plantDataService
) : IStidWorkflowService
{
    private static readonly HttpClient client = new();
    private readonly string _baseUrl =
        configuration["StidUploadWorkflowBaseUrl"]
        ?? throw new InvalidOperationException("StidUploadWorkflowBaseUrl is not configured.");
    private static readonly JsonSerializerOptions useCamelCaseOption = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
    private readonly IPlantDataService plantDataService = plantDataService;

    public async Task TriggerUploadToStid(StidUploadMessage data)
    {
        var stidData = await ReadInspectionStidData(data);
        if (stidData == null)
        {
            return;
        }

        logger.LogInformation(
            "Triggering STID upload workflow for inspection with id {InspectionId}, description {Description}, tag {Tag}, and anonymized blob storage location {BlobStorageLocation}",
            stidData.InspectionId,
            stidData.Description,
            stidData.Tag,
            stidData.AnonymizedBlobStorageLocation
        );

        var postRequestData = new TriggerStidUploadWorkflowRequest(
            stidData.InspectionId,
            stidData.AnonymizedBlobStorageLocation,
            stidData.Tag,
            stidData.Description
        );

        var json = JsonSerializer.Serialize(postRequestData, useCamelCaseOption);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        logger.LogInformation("Posting to STID workflow function at {Url}", _baseUrl);

        try
        {
            var response = await client.PostAsync(_baseUrl, content);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Stid workflow function triggered successfully.");
            }
            else
            {
                logger.LogError(
                    "Failed to trigger stid workflow function with response {Response}.",
                    response.StatusCode
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception occurred while triggering stid workflow function.");
        }
    }

    public async Task<StidData?> ReadInspectionStidData(StidUploadMessage data)
    {
        var plantData = await plantDataService.ReadByInspectionId(data.InspectionId);

        if (plantData == null)
        {
            logger.LogWarning(
                $"Could not find plantData with inspection id {data.InspectionId}, not uploading to STID."
            );
            return null;
        }

        if (plantData.Metadata?.Tag == null || plantData.Metadata.InspectionDescription == null)
        {
            logger.LogWarning(
                $"Could not find required metadata for inspection with id {data.InspectionId}, not uploading to STID."
            );
            return null;
        }

        if (plantData.Metadata.Type != InspectionType.Image)
        {
            logger.LogInformation(
                $"Inspection with id {data.InspectionId} is not of type image, not uploading to STID."
            );
            return null;
        }

        var stidData = new StidData
        {
            InspectionId = data.InspectionId,
            AnonymizedBlobStorageLocation = data.AnonymizedBlobStorageLocation,
            Tag = plantData.Metadata.Tag,
            Description = plantData.Metadata.InspectionDescription,
        };

        await context.StidData.AddAsync(stidData);
        await context.SaveChangesAsync();
        return stidData;
    }

    public async Task<StidData?> UpdateStidWorkflowStatus(
        string inspectionId,
        WorkflowStatus status
    )
    {
        var stidData = await context.StidData.FirstOrDefaultAsync(i =>
            i.InspectionId.Equals(inspectionId)
        );
        if (stidData != null)
        {
            stidData.StidWorkflowStatus = status;
            await context.SaveChangesAsync();
        }
        return stidData;
    }

    public async Task UpdateStidMediaId(string inspectionId, int? mediaId)
    {
        var stidData = await context.StidData.FirstOrDefaultAsync(i =>
            i.InspectionId.Equals(inspectionId)
        );
        if (stidData != null)
        {
            stidData.StidMediaId = mediaId;
            await context.SaveChangesAsync();
        }
    }
}
