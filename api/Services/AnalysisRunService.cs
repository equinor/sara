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
    public string? AnalysisType { get; set; }
    public AnalysisRunStatus? Status { get; set; }
    public DateTime? StartedSince { get; set; }
    public DateTime? StartedUntil { get; set; }
}

public class AnalysisRunService(SaraDbContext context, InstallationAccess access)
    : IAnalysisRunService
{
    public async Task<AnalysisRun?> ReadById(Guid id)
    {
        var run = await access
            .Filter(context.AnalysisRuns, AccessLevel.Observer)
            .Include(r => r.Analysis)
            .Include(r => r.Workflows)
                .ThenInclude(w => w.InputBlobStorageLocations)
            .Include(r => r.Feedback)
            .FirstOrDefaultAsync(r => r.Id == id);
        access.RequireAccessToLookupResult(wasFound: run is not null, AccessLevel.Observer);
        return run;
    }

    public async Task<PagedList<AnalysisRun>> GetRuns(AnalysisRunParameters parameters)
    {
        var query = access
            .Filter(context.AnalysisRuns, AccessLevel.Observer)
            .Include(r => r.Analysis)
            .Include(r => r.Workflows)
            .Include(r => r.Feedback)
            .AsQueryable();

        if (parameters.AnalysisId is { } analysisId)
        {
            access.RequireAccessToLookupResult(
                wasFound: await access
                    .Filter(context.Analyses, AccessLevel.Observer)
                    .AnyAsync(a => a.Id == analysisId),
                AccessLevel.Observer
            );
            query = query.Where(r => r.AnalysisId == analysisId);
        }

        if (!string.IsNullOrWhiteSpace(parameters.AnalysisType))
            query = query.Where(r => r.Analysis.AnalysisType == parameters.AnalysisType);

        if (parameters.Status is { } status)
            query = query.Where(r => r.Status == status);

        if (parameters.StartedSince is { } startedSince)
            query = query.Where(r => r.StartedAt >= startedSince.ToUniversalTime());

        if (parameters.StartedUntil is { } startedUntil)
            query = query.Where(r => r.StartedAt <= startedUntil.ToUniversalTime());

        query = query.OrderByDescending(r => r.StartedAt ?? DateTime.MinValue).ThenBy(r => r.Id);

        return await PagedList<AnalysisRun>.ToPagedListAsync(
            query,
            parameters.PageNumber,
            parameters.PageSize
        );
    }

    public async Task Delete(Guid id)
    {
        var run = await access
            .Filter(context.AnalysisRuns, AccessLevel.Admin)
            .Include(r => r.Workflows)
            .FirstOrDefaultAsync(r => r.Id == id);
        access.RequireAccessToLookupResult(wasFound: run is not null, AccessLevel.Admin);
        if (run is null)
        {
            throw new KeyNotFoundException($"Analysis run with id {id} not found");
        }

        context.Workflows.RemoveRange(run.Workflows);
        context.AnalysisRuns.Remove(run);
        await context.SaveChangesAsync();
    }
}
