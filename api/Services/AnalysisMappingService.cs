using System.Linq.Expressions;
using api.Database.Context;
using api.Database.Models;
using api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

public interface IAnalysisMappingService
{
    public Task<PagedList<AnalysisMapping>> GetAnalysisMappings(
        AnalysisMappingParameters parameters
    );

    public Task<AnalysisMapping?> ReadById(Guid id);

    public Task<AnalysisMapping?> ReadByTagAndInspectionDescription(
        string tagId,
        string inspectionDescription
    );

    public Task<AnalysisMapping> CreateAnalysisMapping(
        string tagId,
        string inspectionPoint,
        AnalysisType analysisType
    );

    public Task<AnalysisMapping> AddAnalysisTypeToMapping(
        AnalysisMapping analysisMapping,
        AnalysisType analysisType
    );

    public Task<List<AnalysisType>> GetAnalysesToBeRun(string tagId, string inspectionDescription);

    public Task<AnalysisMapping> RemoveAnalysisTypeFromMapping(
        Guid analysisMappingId,
        AnalysisType analysisType
    );

    public Task RemoveAnalysisMapping(Guid analysisMappingId);

    public Task<AnalysisMapping> AddOrCreateAnalysisMapping(
        string tagId,
        string inspectionDescription,
        AnalysisType analysisType
    );
}

public class AnalysisMappingService(SaraDbContext context, ILogger<AnalysisMappingService> logger)
    : IAnalysisMappingService
{
    private readonly ILogger<AnalysisMappingService> _logger = logger;

    public async Task<PagedList<AnalysisMapping>> GetAnalysisMappings(
        AnalysisMappingParameters parameters
    )
    {
        var query = context.AnalysisMapping.AsQueryable();

        var filter = ConstructFilter(parameters);
        query = query.Where(filter);
        if (parameters.AnalysisType is not null)
        {
            if (!Enum.TryParse<AnalysisType>(parameters.AnalysisType, true, out var analysisType))
            {
                throw new ArgumentException($"Invalid analysis type: {parameters.AnalysisType}");
            }
            query = query.Where(i => i.AnalysesToBeRun.Contains(analysisType));
        }
        query = query.OrderBy(i => i.Tag);
        return await PagedList<AnalysisMapping>.ToPagedListAsync(
            query,
            parameters.PageNumber,
            parameters.PageSize
        );
    }

    public async Task<AnalysisMapping?> ReadById(Guid id)
    {
        return await context.AnalysisMapping.FirstOrDefaultAsync(i => i.Id.Equals(id));
    }

    public async Task<AnalysisMapping?> ReadByTagAndInspectionDescription(
        string tagId,
        string inspectionDescription
    )
    {
        return await context.AnalysisMapping.FirstOrDefaultAsync(i =>
            i.Tag.ToLower().Equals(tagId.ToLower())
            && inspectionDescription.ToLower().Contains(i.InspectionDescription.ToLower())
        );
    }

    public async Task<AnalysisMapping> CreateAnalysisMapping(
        string tagId,
        string inspectionDescription,
        AnalysisType analysisType
    )
    {
        if (string.IsNullOrEmpty(tagId))
        {
            throw new ArgumentException("TagId cannot be null or empty");
        }
        if (string.IsNullOrEmpty(inspectionDescription))
        {
            throw new ArgumentException("InspectionDescription cannot be null or empty");
        }

        var analysisMapping = new AnalysisMapping(tagId, inspectionDescription, [analysisType]);

        context.AnalysisMapping.Add(analysisMapping);
        await context.SaveChangesAsync();
        return analysisMapping;
    }

    public async Task<AnalysisMapping> AddAnalysisTypeToMapping(
        AnalysisMapping analysisMapping,
        AnalysisType analysisType
    )
    {
        if (analysisMapping.AnalysesToBeRun.Contains(analysisType))
        {
            throw new ArgumentException(
                $"Analysis type {analysisType} already exists in analysis mapping"
            );
        }
        analysisMapping.AnalysesToBeRun.Add(analysisType);
        context.AnalysisMapping.Update(analysisMapping);
        await context.SaveChangesAsync();
        return analysisMapping;
    }

    public async Task<AnalysisMapping> AddOrCreateAnalysisMapping(
        string tagId,
        string inspectionDescription,
        AnalysisType analysisType
    )
    {
        var analysisMapping = await ReadByTagAndInspectionDescription(tagId, inspectionDescription);

        AnalysisMapping updatedAnalysisMapping;
        if (analysisMapping == null)
        {
            updatedAnalysisMapping = await CreateAnalysisMapping(
                tagId,
                inspectionDescription,
                analysisType
            );
        }
        else
        {
            updatedAnalysisMapping = await AddAnalysisTypeToMapping(analysisMapping, analysisType);
        }
        return updatedAnalysisMapping;
    }

    public async Task<List<AnalysisType>> GetAnalysesToBeRun(
        string tagId,
        string inspectionDescription
    )
    {
        tagId = Sanitize.SanitizeUserInput(tagId);
        inspectionDescription = Sanitize.SanitizeUserInput(inspectionDescription);

        var analysisMapping = await ReadByTagAndInspectionDescription(tagId, inspectionDescription);
        _logger.LogInformation(
            "Analysis mapping id for tag '{TagId}' and inspection description '{InspectionDescription}' is {AnalysisMappingId}",
            tagId,
            inspectionDescription,
            analysisMapping?.Id
        );
        return analysisMapping?.AnalysesToBeRun?.ToList() ?? [];
    }

    public async Task<AnalysisMapping> RemoveAnalysisTypeFromMapping(
        Guid analysisMappingId,
        AnalysisType analysisType
    )
    {
        var analysisMapping =
            await ReadById(analysisMappingId)
            ?? throw new ArgumentException(
                $"Analysis mapping with id {analysisMappingId} not found"
            );

        if (!analysisMapping.AnalysesToBeRun.Contains(analysisType))
        {
            throw new ArgumentException(
                $"Analysis type {analysisType} does not exist in analysis mapping"
            );
        }

        analysisMapping.AnalysesToBeRun.Remove(analysisType);
        context.AnalysisMapping.Update(analysisMapping);
        await context.SaveChangesAsync();
        return analysisMapping;
    }

    public async Task RemoveAnalysisMapping(Guid analysisMappingId)
    {
        var analysisMapping =
            await ReadById(analysisMappingId)
            ?? throw new ArgumentException(
                $"Analysis mapping with id {analysisMappingId} not found"
            );

        context.AnalysisMapping.Remove(analysisMapping);
        await context.SaveChangesAsync();
        return;
    }

    /// <summary>
    ///     Filters by <see cref="AnalysisMappingParameters.Tag" />
    ///     and <see cref="AnalysisMappingParameters.InspectionDescription" />
    ///     and <see cref="AnalysisMappingParameters.AnalysisType" />
    ///     <para>
    ///         Uses LINQ Expression trees (see
    ///         <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/expression-trees" />)
    ///     </para>
    /// </summary>
    /// <param name="parameters"> The variable containing the filter params </param>
    private static Expression<Func<AnalysisMapping, bool>> ConstructFilter(
        AnalysisMappingParameters parameters
    )
    {
        Expression<Func<AnalysisMapping, bool>> inspectionDescriptionFilter =
            parameters.InspectionDescription is null
                ? analysisMapping => true
                : analysisMapping =>
                    analysisMapping.InspectionDescription != null
                    && analysisMapping
                        .InspectionDescription.ToLower()
                        .Equals(parameters.InspectionDescription.Trim().ToLower());

        Expression<Func<AnalysisMapping, bool>> tagFilter = parameters.Tag is null
            ? analysisMapping => true
            : analysisMapping =>
                analysisMapping.Tag.ToLower().Equals(parameters.Tag.Trim().ToLower());

        // The parameter of the filter expression
        var analysisMappingExpression = Expression.Parameter(typeof(AnalysisMapping));

        // Combining the body of the filters to create the combined filter, using invoke to force parameter substitution
        Expression body = Expression.AndAlso(
            Expression.Invoke(inspectionDescriptionFilter, analysisMappingExpression),
            Expression.Invoke(tagFilter, analysisMappingExpression)
        );

        // Constructing the resulting lambda expression by combining parameter and body
        return Expression.Lambda<Func<AnalysisMapping, bool>>(body, analysisMappingExpression);
    }
}
