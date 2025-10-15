using System.ComponentModel;
using api.Database.Context;
using api.Database.Models;
using api.MQTT;
using api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

public interface IPlantDataService
{
    public Task<PagedList<PlantData>> GetPlantData(QueryParameters parameters);

    public Task<PlantData?> ReadById(string id);

    public Task<PlantData?> ReadByInspectionId(string inspectionId);

    public Task<List<PlantData>> ReadByTagIdAndInspectionDescription(
        string tagId,
        string inspectionDescription
    );

    public Task<PlantData> CreateFromMqttMessage(
        IsarInspectionResultMessage isarInspectionResultMessage
    );

    public Task<PlantData?> UpdateAnonymizerWorkflowStatus(
        string inspectionId,
        WorkflowStatus status
    );

    public Task<PlantData> CreatePlantDataEntry(PlantDataRequest request);
}

public class PlantDataService(SaraDbContext context, IConfiguration configuration)
    : IPlantDataService
{
    public async Task<PagedList<PlantData>> GetPlantData(QueryParameters parameters)
    {
        var query = context.PlantData.Include(a => a.Analysis).AsQueryable();

        return await PagedList<PlantData>.ToPagedListAsync(
            query,
            parameters.PageNumber,
            parameters.PageSize
        );
    }

    public async Task<PlantData?> ReadById(string id)
    {
        return await context.PlantData.FirstOrDefaultAsync(i => i.Id.Equals(id));
    }

    public async Task<PlantData?> ReadByInspectionId(string inspectionId)
    {
        return await context.PlantData.FirstOrDefaultAsync(i =>
            i.InspectionId.Equals(inspectionId)
        );
    }

    public async Task<List<PlantData>> ReadByTagIdAndInspectionDescription(
        string tagId,
        string inspectionDescription
    )
    {
        return await context
            .PlantData.Where(i =>
                i.Tag != null
                && i.Tag.ToLower().Equals(tagId.ToLower())
                && i.InspectionDescription != null
                && i.InspectionDescription.ToLower().Equals(inspectionDescription.ToLower())
            )
            .ToListAsync();
    }

    public async Task<PlantData> CreateFromMqttMessage(
        IsarInspectionResultMessage isarInspectionResultMessage
    )
    {
        var inspectionDataPath = isarInspectionResultMessage.InspectionDataPath;
        var rawStorageAccount = configuration.GetSection("Storage")["RawStorageAccount"];
        if (!inspectionDataPath.StorageAccount.Equals(rawStorageAccount))
        {
            throw new InvalidOperationException(
                $"Incoming storage account, {inspectionDataPath.StorageAccount}, is not equal to storage account in config, {rawStorageAccount}."
            );
        }
        var rawDataBlobStorageLocation = new BlobStorageLocation
        {
            StorageAccount = inspectionDataPath.StorageAccount,
            BlobContainer = inspectionDataPath.BlobContainer,
            BlobName = inspectionDataPath.BlobName,
        };

        var anonymizedStorageAccount = configuration.GetSection("Storage")["AnonStorageAccount"];
        if (string.IsNullOrEmpty(anonymizedStorageAccount))
        {
            throw new InvalidOperationException("AnonStorageAccount is not configured.");
        }
        var anonymizedDataBlobStorageLocation = new BlobStorageLocation
        {
            StorageAccount = anonymizedStorageAccount,
            BlobContainer = inspectionDataPath.BlobContainer,
            BlobName = inspectionDataPath.BlobName,
        };

        var visualizedStorageAccount = configuration.GetSection("Storage")["VisStorageAccount"];
        if (string.IsNullOrEmpty(visualizedStorageAccount))
        {
            throw new InvalidOperationException("VisualizedStorageAccount is not configured.");
        }
        var visualizedDataBlobStorageLocation = new BlobStorageLocation
        {
            StorageAccount = visualizedStorageAccount,
            BlobContainer = inspectionDataPath.BlobContainer,
            BlobName = inspectionDataPath.BlobName,
        };

        var plantData = new PlantData
        {
            InspectionId = isarInspectionResultMessage.InspectionId,
            RawDataBlobStorageLocation = rawDataBlobStorageLocation,
            AnonymizedBlobStorageLocation = anonymizedDataBlobStorageLocation,
            VisualizedBlobStorageLocation = visualizedDataBlobStorageLocation,
            InstallationCode = isarInspectionResultMessage.InstallationCode,
            Tag = isarInspectionResultMessage.TagID,
            InspectionDescription = isarInspectionResultMessage.InspectionDescription,
            Timestamp = isarInspectionResultMessage.Timestamp,
        };
        await context.PlantData.AddAsync(plantData);
        await context.SaveChangesAsync();
        return plantData;
    }

    public async Task<PlantData> CreatePlantDataEntry(PlantDataRequest request)
    {
        var plantData = new PlantData
        {
            Id = Guid.NewGuid().ToString(),
            InspectionId = request.InspectionId,
            InstallationCode = request.InstallationCode,
            RawDataBlobStorageLocation = request.RawDataBlobStorageLocation,
            AnonymizedBlobStorageLocation = request.AnonymizedBlobStorageLocation,
            VisualizedBlobStorageLocation = request.VisualizedBlobStorageLocation,
            DateCreated = DateTime.UtcNow,
            Tag = request.TagId,
            InspectionDescription = request.InspectionDescription,
            AnonymizerWorkflowStatus = WorkflowStatus.NotStarted,
            AnalysisToBeRun = request.AnalysisToBeRun,
            Analysis = [],
        };

        context.PlantData.Add(plantData);
        await context.SaveChangesAsync();
        return plantData;
    }

    public async Task<PlantData?> UpdateAnonymizerWorkflowStatus(
        string inspectionId,
        WorkflowStatus status
    )
    {
        var plantData = await context.PlantData.FirstOrDefaultAsync(i =>
            i.InspectionId.Equals(inspectionId)
        );
        if (plantData != null)
        {
            plantData.AnonymizerWorkflowStatus = status;
            await context.SaveChangesAsync();
        }
        return plantData;
    }
}
