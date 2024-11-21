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

public class InspectionDataService(IdaDbContext context) : IInspectionDataService
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

        string rawDataUriString = $"{inspectionPath.BlobStorageAccountURL}/" +
                                  $"{inspectionPath.BlobContainer}/" +
                                  $"{inspectionPath.BlobName}";

        var inspectionData = new InspectionData
        {
            InspectionId = isarInspectionResultMessage.InspectionId,
            RawDataUri = new Uri(rawDataUriString),
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
