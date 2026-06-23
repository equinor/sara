using api.Database.Context;
using api.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Utilities;

/// <summary>
/// Resolves <see cref="InspectionRecord"/>s associated with a <see cref="Workflow"/>
/// via the workflow's parent analysis run.
///
/// Use <see cref="GetInspectionRecords"/> for group-aware logic where an analysis
/// may span multiple inspection records. Use <see cref="GetInspectionRecord"/> only
/// in per-record code paths; it warns when more than one record is linked, signalling
/// that the caller should be using the plural form instead.
/// </summary>
public static class InspectionRecordResolver
{
    public static async Task<List<InspectionRecord>> GetInspectionRecords(
        SaraDbContext context,
        Workflow workflow
    )
    {
        return await context
            .InspectionRecords.Where(ir =>
                ir.Analyses.Any(a => a.Runs.Any(r => r.Id == workflow.AnalysisRunId))
            )
            .ToListAsync();
    }

    public static async Task<InspectionRecord?> GetInspectionRecord(
        SaraDbContext context,
        Workflow workflow,
        ILogger logger
    )
    {
        var records = await GetInspectionRecords(context, workflow);
        if (records.Count > 1)
        {
            logger.LogWarning(
                "Workflow {WorkflowId} ({WorkflowType}) resolved to {Count} InspectionRecords "
                    + "but a per-record helper was used. Use GetInspectionRecords for group-aware logic.",
                workflow.Id,
                workflow.WorkflowType,
                records.Count
            );
        }
        return records.FirstOrDefault();
    }

    public static async Task<InspectionRecord?> GetSingleInspectionRecordOrNull(
        SaraDbContext context,
        Workflow workflow,
        string handlerTypeName,
        ILogger logger
    )
    {
        var records = await GetInspectionRecords(context, workflow);

        if (records.Count == 0)
        {
            logger.LogWarning(
                "Workflow {WorkflowType} (Id: {WorkflowId}) has no resolvable InspectionRecord — skipping result handling",
                workflow.WorkflowType,
                workflow.Id
            );
            return null;
        }

        if (records.Count > 1)
        {
            logger.LogWarning(
                "Per-record handler '{HandlerType}' invoked on group analysis ({Count} records) — "
                    + "skipping publish. A group-aware IWorkflowResultHandler must be registered for "
                    + "'{WorkflowType}' if it runs on group analyses.",
                handlerTypeName,
                records.Count,
                workflow.WorkflowType
            );
            return null;
        }

        return records[0];
    }
}
