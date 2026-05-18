using api.Database.Context;
using api.Database.Models;
using api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

public interface IAnalysisRunService
{
    public Task<AnalysisRun?> ReadById(Guid id);

    public Task<PagedList<AnalysisRun>> GetRuns(AnalysisRunParameters parameters);

    public Task Delete(Guid id);
}

public class AnalysisRunParameters
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public Guid? AnalysisId { get; set; }
    public AnalysisRunStatus? Status { get; set; }
}

public class AnalysisRunService(SaraDbContext context) : IAnalysisRunService
{
    public async Task<AnalysisRun?> ReadById(Guid id)
    {
        return await context
            .AnalysisRuns.Include(r => r.Analysis)
            .Include(r => r.Workflows)
            .ThenInclude(w => w.InputBlobStorageLocations)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<PagedList<AnalysisRun>> GetRuns(AnalysisRunParameters parameters)
    {
        var query = context
            .AnalysisRuns.Include(r => r.Analysis)
            .Include(r => r.Workflows)
            .AsQueryable();

        if (parameters.AnalysisId is { } analysisId)
            query = query.Where(r => r.AnalysisId == analysisId);

        if (parameters.Status is { } status)
            query = query.Where(r => r.Status == status);

        query = query.OrderByDescending(r => r.StartedAt ?? DateTime.MinValue).ThenBy(r => r.Id);

        return await PagedList<AnalysisRun>.ToPagedListAsync(
            query,
            parameters.PageNumber,
            parameters.PageSize
        );
    }

    public async Task Delete(Guid id)
    {
        var run = await context
            .AnalysisRuns.Include(r => r.Workflows)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (run is null)
        {
            throw new KeyNotFoundException($"Analysis run with id {id} not found");
        }

        context.Workflows.RemoveRange(run.Workflows);
        context.AnalysisRuns.Remove(run);
        await context.SaveChangesAsync();
    }
}
