using api.Database.Context;
using api.Database.Models;
using api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

public interface IAnalysisService
{
    public Task<Analysis?> ReadById(Guid id);

    public Task<AnalysisRun?> ReadRunById(Guid runId);

    public Task<PagedList<Analysis>> GetAnalyses(AnalysisParameters parameters);

    public Task Delete(Guid id);
}

public class AnalysisParameters
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? Name { get; set; }
    public Guid? AnalysisGroupId { get; set; }
    public Guid? InspectionRecordId { get; set; }
}

public class AnalysisService(SaraDbContext context) : IAnalysisService
{
    public async Task<Analysis?> ReadById(Guid id)
    {
        return await context
            .Analyses.Include(a => a.InspectionRecords)
            .Include(a => a.Runs)
            .ThenInclude(r => r.Workflows)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<AnalysisRun?> ReadRunById(Guid runId)
    {
        return await context
            .AnalysisRuns.Include(r => r.Analysis)
            .Include(r => r.Workflows)
            .FirstOrDefaultAsync(r => r.Id == runId);
    }

    public async Task<PagedList<Analysis>> GetAnalyses(AnalysisParameters parameters)
    {
        var query = context
            .Analyses.Include(a => a.InspectionRecords)
            .Include(a => a.Runs)
            .ThenInclude(r => r.Workflows)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(parameters.Name))
            query = query.Where(a => a.Name.ToLower().Contains(parameters.Name.ToLower()));

        if (parameters.AnalysisGroupId is { } groupId)
            query = query.Where(a => a.AnalysisGroupId == groupId);

        if (parameters.InspectionRecordId is { } recordId)
            query = query.Where(a => a.InspectionRecords.Any(r => r.Id == recordId));

        query = query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id);

        return await PagedList<Analysis>.ToPagedListAsync(
            query,
            parameters.PageNumber,
            parameters.PageSize
        );
    }

    public async Task Delete(Guid id)
    {
        var analysis = await context
            .Analyses.Include(a => a.Runs)
            .ThenInclude(r => r.Workflows)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (analysis is null)
        {
            throw new KeyNotFoundException($"Analysis with id {id} not found");
        }

        foreach (var run in analysis.Runs)
        {
            context.Workflows.RemoveRange(run.Workflows);
        }
        context.AnalysisRuns.RemoveRange(analysis.Runs);
        context.Analyses.Remove(analysis);
        await context.SaveChangesAsync();
    }
}
