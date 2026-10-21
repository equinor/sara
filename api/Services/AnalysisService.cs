using api.Configurations;
using api.Database.Context;
using api.Database.Models;
using api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace api.Services;

public interface IAnalysisService
{
    public Task<Analysis?> ReadById(Guid id);

    public Task<PagedList<Analysis>> GetAnalyses(AnalysisParameters parameters);

    public Task Delete(Guid id);

    public Task<List<string>> GetAvailableAnalyses();
}

public class AnalysisParameters
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? AnalysisType { get; set; }
    public Guid? AnalysisGroupId { get; set; }
    public Guid? InspectionRecordId { get; set; }
}

public class AnalysisService(
    SaraDbContext context,
    IOptions<AnalysisOptions> analysisOptions,
    InstallationAccess access
) : IAnalysisService
{
    private readonly AnalysisOptions _analysisOptions = analysisOptions.Value;

    public async Task<Analysis?> ReadById(Guid id)
    {
        var analysis = await access
            .Filter(context.Analyses, AccessLevel.Observer)
            .Include(a => a.InspectionRecords)
            .Include(a => a.Runs)
                .ThenInclude(r => r.Workflows)
            .FirstOrDefaultAsync(a => a.Id == id);
        access.RequireAccessToLookupResult(wasFound: analysis is not null, AccessLevel.Observer);
        return analysis;
    }

    public async Task<PagedList<Analysis>> GetAnalyses(AnalysisParameters parameters)
    {
        var query = access
            .Filter(context.Analyses, AccessLevel.Observer)
            .Include(a => a.InspectionRecords)
            .Include(a => a.Runs)
                .ThenInclude(r => r.Workflows)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(parameters.AnalysisType))
            query = query.Where(a =>
                a.AnalysisType.ToLower().Contains(parameters.AnalysisType.ToLower())
            );

        if (parameters.AnalysisGroupId is { } groupId)
        {
            access.RequireAccessToLookupResult(
                wasFound: await access
                    .Filter(context.AnalysisGroups, AccessLevel.Observer)
                    .AnyAsync(g => g.Id == groupId),
                AccessLevel.Observer
            );
            query = query.Where(a => a.AnalysisGroupId.Equals(groupId));
        }

        if (parameters.InspectionRecordId is { } recordId)
        {
            access.RequireAccessToLookupResult(
                wasFound: await access
                    .Filter(context.InspectionRecords, AccessLevel.Observer)
                    .AnyAsync(r => r.Id == recordId),
                AccessLevel.Observer
            );
            query = query.Where(a => a.InspectionRecords.Any(r => r.Id == recordId));
        }

        query = query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id);

        return await PagedList<Analysis>.ToPagedListAsync(
            query,
            parameters.PageNumber,
            parameters.PageSize
        );
    }

    public async Task Delete(Guid id)
    {
        var analysis = await access
            .Filter(context.Analyses, AccessLevel.Admin)
            .Include(a => a.Runs)
                .ThenInclude(r => r.Workflows)
            .FirstOrDefaultAsync(a => a.Id == id);

        access.RequireAccessToLookupResult(wasFound: analysis is not null, AccessLevel.Admin);
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

    public Task<List<string>> GetAvailableAnalyses()
    {
        return Task.FromResult(_analysisOptions.Analyses.Keys.ToList());
    }
}
