using api.Models;
using api.Utilities;
using Microsoft.EntityFrameworkCore;
using api.MQTT;

namespace api.Services;

public interface IInspectionDataService
{
    public Task<PagedList<InspectionData>> GetInspectionData(QueryParameters parameters);

    public Task<InspectionData?> ReadById(string id);

    public Task<InspectionData?> ReadByInspectionId(string inspectionId);

    public Task<InspectionData> CreateFromMqttMessage(IsarInspectionResultMessage isarInspectionResultMessage);

    public Task<InspectionData?> UpdateAnonymizerWorkflowStatus(string inspectionId, WorkflowStatus status);
}

public class InspectionDataService(IdaDbContext context, IConfiguration configuration) : IInspectionDataService
{
    public async Task<PagedList<InspectionData>> GetInspectionData(QueryParameters parameters)
    {
        var query = context.InspectionData.Include(a => a.Analysis).AsQueryable();

        return await PagedList<InspectionData>.ToPagedListAsync(
            query,
            parameters.PageNumber,
            parameters.PageSize
        );
    }

    public async Task<InspectionData?> ReadById(string id)
    {
        return await context.InspectionData.FirstOrDefaultAsync(i => i.Id.Equals(id));
    }

    public async Task<InspectionData?> ReadByInspectionId(string inspectionId)
    {
        return await context.InspectionData.FirstOrDefaultAsync(i => i.InspectionId.Equals(inspectionId));
    }

    public async Task<InspectionData> CreateFromMqttMessage(
        IsarInspectionResultMessage isarInspectionResultMessage)
    {
        var inspectionPath = isarInspectionResultMessage.InspectionPath;

        var rawStorageAccount = configuration.GetSection("Storage")["RawStorageAccount"];
        var anonymizedStorageAccount = configuration.GetSection("Storage")["AnonStorageAccount"];

        if (string.IsNullOrEmpty(anonymizedStorageAccount))
        {
            throw new InvalidOperationException("AnonStorageAccount is not configured.");
        }

        if (!inspectionPath.StorageAccount.Equals(rawStorageAccount))
        {
            throw new InvalidOperationException($"Incoming storage account, {inspectionPath.StorageAccount}, is not equal to storage account in config, {rawStorageAccount}.");
        }

        var rawDataBlobStorageLocation = new BlobStorageLocation
        {
            StorageAccount = inspectionPath.StorageAccount,
            BlobContainer = inspectionPath.BlobContainer,
            BlobName = inspectionPath.BlobName
        };

        var anonymizedDataBlobStorageLocation = new BlobStorageLocation
        {
            StorageAccount = anonymizedStorageAccount,
            BlobContainer = inspectionPath.BlobContainer,
            BlobName = inspectionPath.BlobName
        };

        var inspectionData = new InspectionData
        {
            InspectionId = isarInspectionResultMessage.InspectionId,
            RawDataBlobStorageLocation = rawDataBlobStorageLocation,
            AnonymizedBlobStorageLocation = anonymizedDataBlobStorageLocation,
            InstallationCode = isarInspectionResultMessage.InstallationCode,
        };
        await context.InspectionData.AddAsync(inspectionData);
        await context.SaveChangesAsync();
        return inspectionData;
    }
    public async Task<InspectionData?> UpdateAnonymizerWorkflowStatus(string inspectionId, WorkflowStatus status)
    {
        var inspectionData = await context.InspectionData.FirstOrDefaultAsync(i => i.InspectionId.Equals(inspectionId));
        if (inspectionData != null)
        {
            inspectionData.AnonymizerWorkflowStatus = status;
            await context.SaveChangesAsync();
        }
        return inspectionData;
    }
}
