using api.Controllers.Models;
using api.Database.Context;
using api.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

public interface IFeedbackService
{
    Task<AnalysisRunFeedback?> GetByRunId(Guid runId);

    Task<AnalysisRunFeedback> Upsert(Guid runId, UpsertFeedbackRequest request);

    Task Delete(Guid runId);
}

public class FeedbackService(SaraDbContext context) : IFeedbackService
{
    public async Task<AnalysisRunFeedback?> GetByRunId(Guid runId)
    {
        return await context.AnalysisRunFeedbacks.FirstOrDefaultAsync(f =>
            f.AnalysisRunId == runId
        );
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
}
