using api.Models;
using api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

public interface IAnalysisService
{
    public Task<PagedList<Analysis>> GetAnalysis(QueryParameters parameters);

    public Task<Analysis?> ReadById(string id);
}

public class AnalysisService(IdaDbContext context) : IAnalysisService
{
    public async Task<PagedList<Analysis>> GetAnalysis(QueryParameters parameters)
    {
        var query = context.Analysis.AsQueryable();

        return await PagedList<Analysis>.ToPagedListAsync(
            query,
            parameters.PageNumber,
            parameters.PageSize
        );
    }

    public async Task<Analysis?> ReadById(string id)
    {
        return await context.Analysis.FirstOrDefaultAsync(i => i.Id.Equals(id));
    }
}
