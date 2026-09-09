using api.Controllers.Models;
using api.Database.Context;
using api.Database.Models;
using api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

public interface IFeedbackService
{
    Task<AnalysisRunFeedback?> GetByRunId(Guid runId);

    Task<PagedList<FeedbackHistoryDto>> GetAll(FeedbackParameters parameters);

    Task<FeedbackSummaryDto> GetSummary(
        int windowHours,
        TimeZoneInfo timeZone,
        string? analysisType
    );

    Task<FeedbackTrendBucketDetailsDto> GetTrendBucketDetails(
        DateTime bucketStart,
        int windowHours,
        TimeZoneInfo timeZone,
        string? analysisType
    );

    Task<AnalysisRunFeedback> Upsert(Guid runId, UpsertFeedbackRequest request);

    Task Delete(Guid runId);
}

public class FeedbackParameters
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? AnalysisType { get; set; }
    public bool? IsCorrect { get; set; }
    public DateTime? StartedSince { get; set; }
    public DateTime? StartedUntil { get; set; }
}

public class FeedbackService(SaraDbContext context) : IFeedbackService
{
    public async Task<AnalysisRunFeedback?> GetByRunId(Guid runId)
    {
        return await context.AnalysisRunFeedbacks.FirstOrDefaultAsync(f =>
            f.AnalysisRunId == runId
        );
    }

    public async Task<PagedList<FeedbackHistoryDto>> GetAll(FeedbackParameters parameters)
    {
        var query = context.AnalysisRunFeedbacks.AsQueryable();

        if (!string.IsNullOrWhiteSpace(parameters.AnalysisType))
            query = query.Where(feedback =>
                feedback.AnalysisRun.Analysis.AnalysisType == parameters.AnalysisType
            );

        if (parameters.IsCorrect is { } isCorrect)
            query = query.Where(feedback => feedback.IsCorrect == isCorrect);

        if (parameters.StartedSince is { } startedSince)
            query = query.Where(feedback =>
                feedback.AnalysisRun.StartedAt >= startedSince.ToUniversalTime()
            );

        if (parameters.StartedUntil is { } startedUntil)
            query = query.Where(feedback =>
                feedback.AnalysisRun.StartedAt <= startedUntil.ToUniversalTime()
            );

        var projected = query
            .OrderByDescending(feedback => feedback.AnalysisRun.StartedAt ?? DateTime.MinValue)
            .ThenBy(feedback => feedback.Id)
            .Select(feedback => new FeedbackHistoryDto
            {
                Id = feedback.Id,
                AnalysisRunId = feedback.AnalysisRunId,
                AnalysisId = feedback.AnalysisRun.AnalysisId,
                AnalysisType = feedback.AnalysisRun.Analysis.AnalysisType,
                RunNumber = feedback.AnalysisRun.RunNumber,
                RunStatus = feedback.AnalysisRun.Status,
                StartedAt = feedback.AnalysisRun.StartedAt,
                CompletedAt = feedback.AnalysisRun.CompletedAt,
                IsCorrect = feedback.IsCorrect,
            });

        return await PagedList<FeedbackHistoryDto>.ToPagedListAsync(
            projected,
            parameters.PageNumber,
            parameters.PageSize
        );
    }

    public async Task<FeedbackSummaryDto> GetSummary(
        int windowHours,
        TimeZoneInfo timeZone,
        string? analysisType
    )
    {
        var now = DateTime.UtcNow;
        var bucketBoundaries = BuildBucketBoundaries(windowHours, timeZone, now);
        var since = bucketBoundaries[0];
        var until = bucketBoundaries[^1];

        var runsQuery = context.AnalysisRuns.Where(run =>
            run.StartedAt != null && run.StartedAt >= since && run.StartedAt < until
        );
        if (!string.IsNullOrWhiteSpace(analysisType))
            runsQuery = runsQuery.Where(run => run.Analysis.AnalysisType == analysisType);

        var runs = await runsQuery
            .Select(run => new
            {
                run.Analysis.AnalysisType,
                StartedAt = run.StartedAt!.Value,
                IsCorrect = run.Feedback == null ? (bool?)null : run.Feedback.IsCorrect,
            })
            .ToListAsync();

        var reviewed = runs.Where(run => run.IsCorrect != null).ToList();
        return new FeedbackSummaryDto
        {
            WindowHours = windowHours,
            Since = since,
            GeneratedAt = now,
            TotalRuns = runs.Count,
            Reviewed = reviewed.Count,
            Correct = reviewed.Count(run => run.IsCorrect == true),
            Incorrect = reviewed.Count(run => run.IsCorrect == false),
            PerAnalysisType = runs.GroupBy(run => run.AnalysisType)
                .Select(group => new FeedbackAnalysisTypeStatDto
                {
                    AnalysisType = group.Key,
                    TotalRuns = group.Count(),
                    Reviewed = group.Count(run => run.IsCorrect != null),
                    Correct = group.Count(run => run.IsCorrect == true),
                    Incorrect = group.Count(run => run.IsCorrect == false),
                })
                .OrderByDescending(stat => stat.Reviewed)
                .ThenBy(stat => stat.AnalysisType)
                .ToList(),
            Trend = Enumerable
                .Range(0, bucketBoundaries.Count - 1)
                .Select(index =>
                {
                    var bucketStart = bucketBoundaries[index];
                    var bucketEnd = bucketBoundaries[index + 1];
                    var bucketRuns = reviewed.Where(run =>
                        run.StartedAt >= bucketStart && run.StartedAt < bucketEnd
                    );
                    return new FeedbackTrendBucketDto
                    {
                        BucketStart = bucketStart,
                        BucketEnd = bucketEnd,
                        Correct = bucketRuns.Count(run => run.IsCorrect == true),
                        Incorrect = bucketRuns.Count(run => run.IsCorrect == false),
                    };
                })
                .ToList(),
        };
    }

    public async Task<FeedbackTrendBucketDetailsDto> GetTrendBucketDetails(
        DateTime bucketStart,
        int windowHours,
        TimeZoneInfo timeZone,
        string? analysisType
    )
    {
        bucketStart = bucketStart.ToUniversalTime();
        var bucketEnd = GetBucketEnd(bucketStart, windowHours, timeZone);
        var query = context.AnalysisRuns.Where(run =>
            run.StartedAt != null
            && run.StartedAt >= bucketStart
            && run.StartedAt < bucketEnd
            && run.Feedback != null
        );

        if (!string.IsNullOrWhiteSpace(analysisType))
            query = query.Where(run => run.Analysis.AnalysisType == analysisType);

        var stats = await query
            .GroupBy(run => run.Analysis.AnalysisType)
            .Select(group => new FeedbackTrendAnalysisTypeStatDto
            {
                AnalysisType = group.Key,
                Correct = group.Count(run => run.Feedback!.IsCorrect),
                Incorrect = group.Count(run => !run.Feedback!.IsCorrect),
            })
            .OrderByDescending(stat => stat.Correct + stat.Incorrect)
            .ThenBy(stat => stat.AnalysisType)
            .ToListAsync();

        return new FeedbackTrendBucketDetailsDto
        {
            BucketStart = bucketStart,
            BucketEnd = bucketEnd,
            PerAnalysisType = stats,
        };
    }

    public async Task<AnalysisRunFeedback> Upsert(Guid runId, UpsertFeedbackRequest request)
    {
        var runExists = await context.AnalysisRuns.AnyAsync(r => r.Id == runId);
        if (!runExists)
            throw new KeyNotFoundException($"Analysis run with id {runId} not found");

        var existing = await context.AnalysisRunFeedbacks.FirstOrDefaultAsync(f =>
            f.AnalysisRunId == runId
        );

        if (existing is null)
        {
            var feedback = new AnalysisRunFeedback
            {
                AnalysisRunId = runId,
                AnalysisRun = null!,
                IsCorrect = request.IsCorrect,
            };
            context.AnalysisRunFeedbacks.Add(feedback);
            await context.SaveChangesAsync();
            return feedback;
        }

        existing.IsCorrect = request.IsCorrect;
        await context.SaveChangesAsync();
        return existing;
    }

    public async Task Delete(Guid runId)
    {
        var feedback = await context.AnalysisRunFeedbacks.FirstOrDefaultAsync(f =>
            f.AnalysisRunId == runId
        );
        if (feedback is null)
            throw new KeyNotFoundException($"No feedback found for analysis run with id {runId}");

        context.AnalysisRunFeedbacks.Remove(feedback);
        await context.SaveChangesAsync();
    }

    private static List<DateTime> BuildBucketBoundaries(
        int windowHours,
        TimeZoneInfo timeZone,
        DateTime now
    )
    {
        if (windowHours <= 24)
        {
            var currentHour = new DateTime(
                now.Year,
                now.Month,
                now.Day,
                now.Hour,
                0,
                0,
                DateTimeKind.Utc
            );
            return Enumerable
                .Range(0, windowHours + 1)
                .Select(index => currentHour.AddHours(index - windowHours + 1))
                .ToList();
        }

        var dayCount = windowHours / 24;
        var localToday = TimeZoneInfo.ConvertTimeFromUtc(now, timeZone).Date;
        return Enumerable
            .Range(0, dayCount + 1)
            .Select(index =>
                TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(
                        localToday.AddDays(index - dayCount + 1),
                        DateTimeKind.Unspecified
                    ),
                    timeZone
                )
            )
            .ToList();
    }

    private static DateTime GetBucketEnd(
        DateTime bucketStart,
        int windowHours,
        TimeZoneInfo timeZone
    )
    {
        if (windowHours <= 24)
            return bucketStart.AddHours(1);

        var localDate = TimeZoneInfo.ConvertTimeFromUtc(bucketStart, timeZone).Date;
        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localDate.AddDays(1), DateTimeKind.Unspecified),
            timeZone
        );
    }
}
