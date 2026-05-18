using api.Database.Context;
using api.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

public interface IAnalysisGroupTimeoutProcessor
{
    Task ProcessTimedOutGroups(CancellationToken cancellationToken);
}

public class AnalysisGroupTimeoutProcessor(
    SaraDbContext context,
    ILogger<AnalysisGroupTimeoutProcessor> logger
) : IAnalysisGroupTimeoutProcessor
{
    public async Task ProcessTimedOutGroups(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var timedOutGroups = await context
            .AnalysisGroups.Where(g =>
                g.Status == AnalysisGroupStatus.Pending && g.TimeoutAt != null && g.TimeoutAt <= now
            )
            .ToListAsync(cancellationToken);

        if (timedOutGroups.Count == 0)
        {
            return;
        }

        foreach (var group in timedOutGroups)
        {
            var receivedCount = await context.InspectionRecords.CountAsync(
                ir => ir.AnalysisGroupId == group.Id,
                cancellationToken
            );

            logger.LogWarning(
                "AnalysisGroup {GroupId} timed out at {TimeoutAt}: received {ReceivedCount}/{ExpectedSize} records",
                group.GroupId,
                group.TimeoutAt,
                receivedCount,
                group.ExpectedSize
            );

            group.Status = AnalysisGroupStatus.TimedOut;
            await context.SaveChangesAsync(cancellationToken);

            var abandonedAnalyses = await context
                .Analyses.Include(a => a.Runs)
                .Where(a => a.AnalysisGroupId == group.Id && a.Runs.Count == 0)
                .ToListAsync(cancellationToken);

            if (abandonedAnalyses.Count == 0)
            {
                continue;
            }

            logger.LogWarning(
                "AnalysisGroup {GroupId} timed out with {AbandonedCount} deferred analyses that will NOT run: {AnalysisNames}",
                group.GroupId,
                abandonedAnalyses.Count,
                string.Join(", ", abandonedAnalyses.Select(a => $"{a.Name} ({a.Id})"))
            );
        }
    }
}
