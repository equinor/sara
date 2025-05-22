using api.Database.Context;
using api.Database.Models;
using api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

public interface IAnalysisMappingService
{
    public Task<PagedList<AnalysisMapping>> GetAnalysisMappings(QueryParameters parameters);

    public Task<AnalysisMapping?> ReadById(string id);

    public Task<AnalysisMapping> CreateAnalysisMapping(
        string tagId,
        string inspectionPoint,
        AnalysisType? analysisType
    );

    public Task<AnalysisMapping> AddAnalysisTypeToMapping(
        string analysisMappingId,
        string analysisType
    );

    public AnalysisType InspectionDescriptionToAnalysisType(
        string inspectionDescription,
        string tagId
    );
}

public class AnalysisMappingService(IdaDbContext context) : IAnalysisMappingService
{
    public async Task<PagedList<AnalysisMapping>> GetAnalysisMappings(QueryParameters parameters)
    {
        var query = context.AnalysisMapping.AsQueryable();

        return await PagedList<AnalysisMapping>.ToPagedListAsync(
            query,
            parameters.PageNumber,
            parameters.PageSize
        );
    }

    public async Task<AnalysisMapping?> ReadById(string id)
    {
        return await context.AnalysisMapping.FirstOrDefaultAsync(i => i.Id.Equals(id));
    }

    public async Task<AnalysisMapping> CreateAnalysisMapping(
        string tagId,
        string inspectionDescription,
        AnalysisType? analysisType
    )
    {
        if (string.IsNullOrEmpty(tagId))
        {
            throw new ArgumentException($"TagId cannot be null or empty");
        }
        if (string.IsNullOrEmpty(inspectionDescription))
        {
            throw new ArgumentException($"InspectionDescription cannot be null or empty");
        }

        var analysisMapping = new AnalysisMapping(tagId, inspectionDescription);

        if (analysisType != null)
        {
            analysisMapping.AnalysesToBeRun.Add((AnalysisType)analysisType);
        }

        context.AnalysisMapping.Add(analysisMapping);
        await context.SaveChangesAsync();
        return analysisMapping;
    }

    public Task<AnalysisMapping> AddAnalysisTypeToMapping(
        string analysisMappingId,
        string analysisType
    )
    {
        var analysisMapping =
            context.AnalysisMapping.FirstOrDefault(i => i.Id.Equals(analysisMappingId))
            ?? throw new ArgumentException(
                $"Analysis mapping with id {analysisMappingId} not found"
            );

        var analysisTypeEnum =
            Analysis.TypeFromString(analysisType)
            ?? throw new ArgumentException(
                $"Analysis type {analysisType} already exists in analysis mapping"
            );

        analysisMapping.AnalysesToBeRun.Add(analysisTypeEnum);
        context.AnalysisMapping.Update(analysisMapping);
        context.SaveChanges();
        return Task.FromResult(analysisMapping);
    }

    public AnalysisType InspectionDescriptionToAnalysisType(
        string inspectionDescription,
        string tagId
    )
    {
        return inspectionDescription switch
        {
            "anonymize" => AnalysisType.Anonymize,
            _ => throw new ArgumentException(
                $"Failed to parse task status '{inspectionDescription}' - not supported"
            ),
        };
    }
}
