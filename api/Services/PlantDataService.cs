using System.Net.Http.Headers;
using api.Database.Context;
using api.Database.Models;
using api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

public interface IPlantDataService
{
    public Task<PagedList<PlantData>> GetPlantData(QueryParameters parameters);

    public Task<PlantData?> ReadById(string id);

    public Task<PlantData?> ReadByInspectionId(string inspectionId);

    public Task<PlantData?> UpdateAnonymizerWorkflowStatus(
        string inspectionId,
        WorkflowStatus status
    );
    public Task<PlantData?> UpdateCLOEWorkflowStatus(string inspectionId, WorkflowStatus status);
    public Task<PlantData?> UpdateCLOEResult(string inspectionId, float oilLevel);
    public Task WritePlantData(PlantData plantData);
    public Task<PlantData?> UpdateFencillaWorkflowStatus(string inspectionId, WorkflowStatus started);
    public Task<PlantData?> UpdateFencillaResult(string inspectionId, bool isBreak, float confidence);
}

public class PlantDataService(SaraDbContext context) : IPlantDataService
{
    public async Task<PagedList<PlantData>> GetPlantData(QueryParameters parameters)
    {
        var query = context.PlantData.AsQueryable();

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

    public async Task WritePlantData(PlantData plantData)
    {
        await context.PlantData.AddAsync(plantData);
        await context.SaveChangesAsync();
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
            plantData.Anonymization.Status = status;
            await context.SaveChangesAsync();
        }
        return plantData;
    }

    public async Task<PlantData?> UpdateCLOEWorkflowStatus(string inspectionId, WorkflowStatus status)
    {
        var plantData = await context.PlantData.FirstOrDefaultAsync(i =>
            i.InspectionId.Equals(inspectionId)
        );
        if (plantData != null)
        {
            if (plantData.CLOEAnalysis == null)
            {
                throw new InvalidOperationException(
                    $"CLOE analysis is not set up for plant data with inspection id {inspectionId}"
                );
            }
            plantData.CLOEAnalysis.Status = status;
            await context.SaveChangesAsync();
        }
        return plantData;
    }

    public async Task<PlantData?> UpdateCLOEResult(string inspectionId, float oilLevel)
    {
        var plantData = await context.PlantData.FirstOrDefaultAsync(i =>
            i.InspectionId.Equals(inspectionId)
        );
        if (plantData != null)
        {
            if (plantData.CLOEAnalysis == null)
            {
                throw new InvalidOperationException(
                    $"CLOE analysis is not set up for plant data with inspection id {inspectionId}"
                );
            }
            plantData.CLOEAnalysis.Result = new CLOEResult { OilLevel = oilLevel };
            await context.SaveChangesAsync();
        }
        return plantData;
    }

    public async Task<PlantData?> UpdateFencillaWorkflowStatus(string inspectionId, WorkflowStatus status)
    {
        var plantData = await context.PlantData.FirstOrDefaultAsync(i =>
            i.InspectionId.Equals(inspectionId)
        );
        if (plantData != null)
        {
            if (plantData.FencillaAnalysis == null)
            {
                throw new InvalidOperationException(
                    $"Fencilla analysis is not set up for plant data with inspection id {inspectionId}"
                );
            }
            plantData.FencillaAnalysis.Status = status;
            await context.SaveChangesAsync();
        }
        return plantData;
    }

    public async Task<PlantData?> UpdateFencillaResult(string inspectionId, bool isBreak, float confidence)
    {
        var plantData = await context.PlantData.FirstOrDefaultAsync(i =>
            i.InspectionId.Equals(inspectionId)
        );
        if (plantData != null)
        {
            if (plantData.FencillaAnalysis == null)
            {
                throw new InvalidOperationException(
                    $"Fencilla analysis is not set up for plant data with inspection id {inspectionId}"
                );
            }
            plantData.FencillaAnalysis.Result = new FencillaResult { IsBreak = isBreak, Confidence = confidence };
            await context.SaveChangesAsync();
        }
        return plantData;
    }
}
