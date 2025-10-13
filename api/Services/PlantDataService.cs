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

    public Task<PlantData> CreateFromMqttMessage(
        IsarInspectionResultMessage isarInspectionResultMessage
    );

    public Task<PlantData?> UpdateAnonymizerWorkflowStatus(
        string inspectionId,
        WorkflowStatus status
    );
}

public class PlantDataService(
    ILogger<PlantDataService> logger,
    SaraDbContext context,
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory
) : IPlantDataService
{
    private readonly ILogger<PlantDataService> _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    private IAnalysisMappingService AnalysisMappingService =>
        _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<IAnalysisMappingService>();

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

    private static string PostfixAnalysisTypeToBlobName(string blobName, string analsyisTypePostfix)
    {
        var blobNameComponents = blobName.Split(".");
        if (blobNameComponents.Length != 2)
        {
            throw new InvalidOperationException(
                $"Invalid blobName, containing multiple dots: {blobName}"
            );
        }

        return blobNameComponents[0] + "_" + analsyisTypePostfix + "." + blobNameComponents[1];
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

        List<AnalysisType> analysisToBeRun;
        try
        {
            analysisToBeRun =
                await AnalysisMappingService.GetAnalysisTypeFromInspectionDescriptionAndTag(
                    isarInspectionResultMessage.InspectionDescription,
                    isarInspectionResultMessage.TagID
                );
        }
        catch (Exception)
        {
            throw new InvalidOperationException("Error occurred while fetching analysis mapping");
        }

        var visualizedStorageAccount = configuration.GetSection("Storage")["VisStorageAccount"];
        if (string.IsNullOrEmpty(visualizedStorageAccount))
        {
            throw new InvalidOperationException("VisStorageAccount is not configured.");
        }

        List<Analysis> Analyses = [];
        if (analysisToBeRun.Contains(AnalysisType.ConstantLevelOilerEstimator))
        {
            _logger.LogInformation(
                "Analysis type ConstantLevelOilerEstimator is set to be run for InspectionId: {InspectionId}",
                isarInspectionResultMessage.InspectionId
            );
            var visualizedBlobStorageLocation = new BlobStorageLocation
            {
                StorageAccount = visualizedStorageAccount,
                BlobContainer = inspectionDataPath.BlobContainer,
                BlobName = PostfixAnalysisTypeToBlobName(inspectionDataPath.BlobName, "cloe"),
            };
            Analyses.Add(
                new Analysis
                {
                    Type = AnalysisType.ConstantLevelOilerEstimator,
                    SourceBlobStorageLocation = anonymizedDataBlobStorageLocation,
                    VisualizedBlobStorageLocation = visualizedBlobStorageLocation,
                }
            );
        }
        if (analysisToBeRun.Contains(AnalysisType.Fencilla))
        {
            _logger.LogInformation(
                "Analysis type Fencilla is set to be run for InspectionId: {InspectionId}",
                isarInspectionResultMessage.InspectionId
            );
            var visualizedBlobStorageLocation = new BlobStorageLocation
            {
                StorageAccount = visualizedStorageAccount,
                BlobContainer = inspectionDataPath.BlobContainer,
                BlobName = PostfixAnalysisTypeToBlobName(inspectionDataPath.BlobName, "fencilla"),
            };
            Analyses.Add(
                new Analysis
                {
                    Type = AnalysisType.Fencilla,
                    SourceBlobStorageLocation = anonymizedDataBlobStorageLocation,
                    VisualizedBlobStorageLocation = visualizedBlobStorageLocation,
                }
            );
        }

        var plantData = new PlantData
        {
            InspectionId = isarInspectionResultMessage.InspectionId,
            Anonymization = new Anonymization
            {
                RawDataBlobStorageLocation = rawDataBlobStorageLocation,
                AnonymizedBlobStorageLocation = anonymizedDataBlobStorageLocation,
            },
            InstallationCode = isarInspectionResultMessage.InstallationCode,
            Analysis = Analyses,
        };
        await context.PlantData.AddAsync(plantData);
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
            plantData.Anonymization.Status = status;
            await context.SaveChangesAsync();
        }
        return plantData;
    }
}
