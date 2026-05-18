using api.Database.Context;
using api.Database.Models;
using api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

public interface IAnalysisGroupService
{
    public Task<AnalysisGroup?> ReadById(Guid id);

    public Task<PagedList<AnalysisGroup>> GetGroups(AnalysisGroupParameters parameters);

    public Task Delete(Guid id);
}

public class AnalysisGroupParameters
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? GroupId { get; set; }
    public AnalysisGroupStatus? Status { get; set; }
}

public class AnalysisGroupService(SaraDbContext context) : IAnalysisGroupService
{
    public async Task<AnalysisGroup?> ReadById(Guid id)
    {
        return await context
            .AnalysisGroups.Include(g => g.InspectionRecords)
            .Include(g => g.Analyses)
            .ThenInclude(a => a.Runs)
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<PagedList<AnalysisGroup>> GetGroups(AnalysisGroupParameters parameters)
    {
        var query = context
            .AnalysisGroups.Include(g => g.InspectionRecords)
            .Include(g => g.Analyses)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(parameters.GroupId))
            query = query.Where(g => g.GroupId.ToLower().Contains(parameters.GroupId.ToLower()));

        if (parameters.Status is { } status)
            query = query.Where(g => g.Status == status);

        query = query.OrderByDescending(g => g.Id);

        return await PagedList<AnalysisGroup>.ToPagedListAsync(
            query,
            parameters.PageNumber,
            parameters.PageSize
        );
    }

    public async Task Delete(Guid id)
    {
        var group = await context
            .AnalysisGroups.Include(g => g.Analyses)
            .ThenInclude(a => a.Runs)
            .ThenInclude(r => r.Workflows)
            .Include(g => g.InspectionRecords)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group is null)
        {
            throw new KeyNotFoundException($"Analysis group with id {id} not found");
        }

        foreach (var record in group.InspectionRecords)
        {
            record.AnalysisGroupId = null;
        }

        foreach (var analysis in group.Analyses)
        {
            foreach (var run in analysis.Runs)
            {
                context.Workflows.RemoveRange(run.Workflows);
            }
            context.AnalysisRuns.RemoveRange(analysis.Runs);
        }
        context.Analyses.RemoveRange(group.Analyses);
        context.AnalysisGroups.Remove(group);
        await context.SaveChangesAsync();
    }
}
