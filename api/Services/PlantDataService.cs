using api.Database.Context;
using api.Database.Models;
using api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

public interface IPlantDataService
{
    public Task<PagedList<PlantData>> GetPlantData(QueryParameters parameters);

    public Task<PlantData?> ReadById(string id);

    public Task<List<PlantData>> ReadByTagIdAndInspectionDescription(
        string tagId,
        string inspectionDescription
    );
    public Task<bool> ExistsByInspectionId(string inspectionId);

    public Task<PlantData> ReadByInspectionId(string inspectionId);

    public Task<PlantData> CreatePlantData(
        string inspectionId,
        string installationCode,
        string tagID,
        string inspectionDescription,
        string rawStorageAccount,
        string rawBlobContainer,
        string rawBlobName
    );

    public Task WritePlantData(PlantData plantData);

    public Task<PlantData> UpdateAnonymizerWorkflowStatus(
        string inspectionId,
        WorkflowStatus status
    );

    public Task<PlantData> UpdateCLOEWorkflowStatus(string inspectionId, WorkflowStatus status);

    public Task<PlantData> UpdateFencillaWorkflowStatus(
        string inspectionId,
        WorkflowStatus started
    );

    public Task<PlantData> UpdateAnonymizerResult(string inspectionId, bool isPersonInImage);

    public Task<PlantData> UpdateCLOEResult(string inspectionId, float oilLevel);

    public Task<PlantData> UpdateFencillaResult(
        string inspectionId,
        bool isBreak,
        float confidence
    );
    public Task UpdatePlantDataFromAnalysisMapping(
        string tagId,
        string inspectionDescription,
        AnalysisType analysisType
    );
}

public class PlantDataService(
    SaraDbContext context,
    IAnalysisMappingService analysisMappingService,
    IBlobService blobService,
    ILogger<PlantDataService> logger
) : IPlantDataService
{
    public async Task<PagedList<PlantData>> GetPlantData(QueryParameters parameters)
    {
        var query = context
            .PlantData.Include(plantData => plantData.Anonymization)
            .Include(plantData => plantData.CLOEAnalysis)
            .Include(plantData => plantData.FencillaAnalysis)
            .AsQueryable();

        return await PagedList<PlantData>.ToPagedListAsync(
            query,
            parameters.PageNumber,
            parameters.PageSize
        );
    }

    public async Task<PlantData?> ReadById(string id)
    {
        return await context
            .PlantData.Include(plantData => plantData.Anonymization)
            .Include(plantData => plantData.CLOEAnalysis)
            .Include(plantData => plantData.FencillaAnalysis)
            .FirstOrDefaultAsync(i => i.Id.Equals(id));
    }

    public async Task<bool> ExistsByInspectionId(string inspectionId)
    {
        return await context.PlantData.AnyAsync(i => i.InspectionId.Equals(inspectionId));
    }

    public async Task<List<PlantData>> ReadByTagIdAndInspectionDescription(
        string tagId,
        string inspectionDescription
    )
    {
        return await context
            .PlantData.Include(plantData => plantData.Anonymization)
            .Include(plantData => plantData.CLOEAnalysis)
            .Include(plantData => plantData.FencillaAnalysis)
            .Where(i =>
                i.Tag != null
                && i.Tag.ToLower().Equals(tagId.ToLower())
                && i.InspectionDescription != null
                && i.InspectionDescription.ToLower().Equals(inspectionDescription.ToLower())
            )
            .ToListAsync();
    }

    public async Task<PlantData> ReadByInspectionId(string inspectionId)
    {
        var plantData = await context
            .PlantData.Include(plantData => plantData.Anonymization)
            .Include(plantData => plantData.CLOEAnalysis)
            .Include(plantData => plantData.FencillaAnalysis)
            .FirstOrDefaultAsync(i => i.InspectionId.Equals(inspectionId));
        if (plantData == null)
        {
            throw new InvalidOperationException(
                $"Could not find plantData with inspection id {inspectionId}"
            );
        }
        return plantData;
    }

    public async Task<PlantData> CreatePlantData(
        string inspectionId,
        string installationCode,
        string tagID,
        string inspectionDescription,
        string rawStorageAccount,
        string rawBlobContainer,
        string rawBlobName
    )
    {
        List<AnalysisType> analysisToBeRun;
        try
        {
            analysisToBeRun = await analysisMappingService.GetAnalysesToBeRun(
                tagID,
                inspectionDescription
            );
        }
        catch (Exception)
        {
            throw new InvalidOperationException("Error occurred while fetching analysis mapping");
        }

        var anonymization = new Anonymization
        {
            SourceBlobStorageLocation = blobService.CreateRawBlobStorageLocation(
                rawStorageAccount,
                rawBlobContainer,
                rawBlobName
            ),
            DestinationBlobStorageLocation = blobService.CreateAnonymizedBlobStorageLocation(
                rawBlobContainer,
                rawBlobName
            ),
        };

        var cloeAnalysis = null as CLOEAnalysis;
        if (analysisToBeRun.Contains(AnalysisType.ConstantLevelOiler))
        {
            logger.LogInformation(
                "Analysis type ConstantLevelOilerEstimator is set to be run for InspectionId: {InspectionId}",
                inspectionId
            );
            cloeAnalysis = new CLOEAnalysis
            {
                SourceBlobStorageLocation = blobService.CreateAnonymizedBlobStorageLocation(
                    rawBlobContainer,
                    rawBlobName
                ),
                DestinationBlobStorageLocation = blobService.CreateVisualizedBlobStorageLocation(
                    rawBlobContainer,
                    rawBlobName,
                    "cloe"
                ),
            };
        }

        var fencillaAnalysis = null as FencillaAnalysis;
        if (analysisToBeRun.Contains(AnalysisType.Fencilla))
        {
            logger.LogInformation(
                "Analysis type Fencilla is set to be run for InspectionId: {InspectionId}",
                inspectionId
            );
            fencillaAnalysis = new FencillaAnalysis
            {
                SourceBlobStorageLocation = blobService.CreateAnonymizedBlobStorageLocation(
                    rawBlobContainer,
                    rawBlobName
                ),
                DestinationBlobStorageLocation = blobService.CreateVisualizedBlobStorageLocation(
                    rawBlobContainer,
                    rawBlobName,
                    "fencilla"
                ),
            };
        }

        var plantData = new PlantData
        {
            InspectionId = inspectionId,
            InstallationCode = installationCode,
            Tag = tagID,
            InspectionDescription = inspectionDescription,
            Anonymization = anonymization,
            CLOEAnalysis = cloeAnalysis,
            FencillaAnalysis = fencillaAnalysis,
        };
        await WritePlantData(plantData);
        return plantData;
    }

    public async Task WritePlantData(PlantData plantData)
    {
        await context.PlantData.AddAsync(plantData);
        await context.SaveChangesAsync();
    }

    public async Task<PlantData> UpdateAnonymizerWorkflowStatus(
        string inspectionId,
        WorkflowStatus status
    )
    {
        var plantData = await ReadByInspectionId(inspectionId);
        plantData.Anonymization.Status = status;
        await context.SaveChangesAsync();
        return plantData;
    }

    public async Task<PlantData> UpdateCLOEWorkflowStatus(
        string inspectionId,
        WorkflowStatus status
    )
    {
        var plantData = await ReadByInspectionId(inspectionId);
        if (plantData.CLOEAnalysis == null)
        {
            throw new InvalidOperationException(
                $"CLOE analysis is not set up for plant data with inspection id {inspectionId}"
            );
        }
        plantData.CLOEAnalysis.Status = status;
        await context.SaveChangesAsync();
        return plantData;
    }

    public async Task<PlantData> UpdateFencillaWorkflowStatus(
        string inspectionId,
        WorkflowStatus status
    )
    {
        var plantData = await ReadByInspectionId(inspectionId);
        if (plantData.FencillaAnalysis == null)
        {
            throw new InvalidOperationException(
                $"Fencilla analysis is not set up for plant data with inspection id {inspectionId}"
            );
        }
        plantData.FencillaAnalysis.Status = status;
        await context.SaveChangesAsync();
        return plantData;
    }

    public async Task<PlantData> UpdateAnonymizerResult(string inspectionId, bool isPersonInImage)
    {
        var plantData = await ReadByInspectionId(inspectionId);
        plantData.Anonymization.IsPersonInImage = isPersonInImage;
        await context.SaveChangesAsync();
        return plantData;
    }

    public async Task<PlantData> UpdateCLOEResult(string inspectionId, float oilLevel)
    {
        var plantData = await ReadByInspectionId(inspectionId);
        if (plantData.CLOEAnalysis == null)
        {
            throw new InvalidOperationException(
                $"CLOE analysis is not set up for plant data with inspection id {inspectionId}"
            );
        }
        if (oilLevel < 0 || oilLevel > 100)
        {
            throw new InvalidOperationException(
                $"Invalid oil level {oilLevel} received for inspection id {inspectionId}. Must be between 0 and 100."
            );
        }
        plantData.CLOEAnalysis.OilLevel = oilLevel;
        await context.SaveChangesAsync();
        return plantData;
    }

    public async Task<PlantData> UpdateFencillaResult(
        string inspectionId,
        bool isBreak,
        float confidence
    )
    {
        var plantData = await ReadByInspectionId(inspectionId);
        if (plantData.FencillaAnalysis == null)
        {
            throw new InvalidOperationException(
                $"Fencilla analysis is not set up for plant data with inspection id {inspectionId}"
            );
        }
        if (isBreak.GetType() != typeof(bool))
        {
            throw new InvalidOperationException(
                $"Invalid IsBreak value {isBreak} received for inspection id {inspectionId}. Must be a boolean."
            );
        }
        if (confidence < 0 || confidence > 1)
        {
            throw new InvalidOperationException(
                $"Invalid Confidence value {confidence} received for inspection id {inspectionId}. Must be between 0 and 1."
            );
        }
        plantData.FencillaAnalysis.IsBreak = isBreak;
        plantData.FencillaAnalysis.Confidence = confidence;
        await context.SaveChangesAsync();
        return plantData;
    }

    public async Task UpdatePlantDataFromAnalysisMapping(
        string tagId,
        string inspectionDescription,
        AnalysisType analysisType
    )
    {
        var plantDataEntries = await ReadByTagIdAndInspectionDescription(
            tagId,
            inspectionDescription
        );

        if (plantDataEntries.Count == 0)
        {
            return;
        }

        foreach (var plantData in plantDataEntries)
        {
            var blobContainer = plantData
                .Anonymization
                .DestinationBlobStorageLocation
                .BlobContainer;
            var blobName = plantData.Anonymization.DestinationBlobStorageLocation.BlobName;

            if (analysisType == AnalysisType.ConstantLevelOiler && plantData.CLOEAnalysis is null)
            {
                plantData.CLOEAnalysis = new CLOEAnalysis
                {
                    SourceBlobStorageLocation = blobService.CreateAnonymizedBlobStorageLocation(
                        blobContainer,
                        blobName
                    ),
                    DestinationBlobStorageLocation =
                        blobService.CreateVisualizedBlobStorageLocation(
                            blobContainer,
                            blobName,
                            "cloe"
                        ),
                };
            }

            if (analysisType == AnalysisType.Fencilla && plantData.FencillaAnalysis is null)
            {
                plantData.FencillaAnalysis = new FencillaAnalysis
                {
                    SourceBlobStorageLocation = blobService.CreateAnonymizedBlobStorageLocation(
                        blobContainer,
                        blobName
                    ),
                    DestinationBlobStorageLocation =
                        blobService.CreateVisualizedBlobStorageLocation(
                            blobContainer,
                            blobName,
                            "fencilla"
                        ),
                };
            }

            context.PlantData.Update(plantData);
            await context.SaveChangesAsync();
        }
    }
}
