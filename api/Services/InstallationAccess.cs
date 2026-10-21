using System.Security.Claims;
using api.Database.Models;

namespace api.Services;

public enum AccessLevel
{
    Observer,
    Admin,
}

public class InstallationAccessException() : Exception("Installation access denied");

public class InstallationAccess(
    IHttpContextAccessor httpContextAccessor,
    ILogger<InstallationAccess> logger
)
{
    // Mainly reads within assigned installations, plus non-critical workflow metadata writes
    // such as feedback. Administrators also satisfy this policy.
    public const string ObserverPolicy = "Installation.Observer";

    // Full access to all read and write operations across installations.
    public const string AdminPolicy = "Installation.Admin";

    // No HTTP context is trusted background processing; a missing auth header still has a context.
    public bool HasFullAccess(AccessLevel level) =>
        httpContextAccessor.HttpContext is null
        || HasGlobalAccess(httpContextAccessor.HttpContext.User);

    public string[] InstallationCodes(AccessLevel level) =>
        GetInstallationCodes(httpContextAccessor.HttpContext?.User, level);

    public void RequireAccess(string installationCode, AccessLevel level) =>
        Require(
            HasFullAccess(level)
                || InstallationCodes(level).Contains(installationCode.ToUpperInvariant())
        );

    public void RequireAccessToLookupResult(bool wasFound, AccessLevel level) =>
        Require(wasFound || HasFullAccess(level));

    private void Require(bool permitted)
    {
        if (permitted)
            return;

        logger.LogWarning("Installation access denied");
        throw new InstallationAccessException();
    }

    public IQueryable<InspectionRecord> Filter(
        IQueryable<InspectionRecord> query,
        AccessLevel level
    )
    {
        if (HasFullAccess(level))
            return query;
        var codes = InstallationCodes(level);
        return query.Where(record => codes.Contains(record.InstallationCode.ToUpper()));
    }

    public IQueryable<Analysis> Filter(IQueryable<Analysis> query, AccessLevel level)
    {
        if (HasFullAccess(level))
            return query;
        var codes = InstallationCodes(level);
        return query.Where(analysis =>
            analysis.InspectionRecords.Any()
            && analysis.InspectionRecords.All(record =>
                codes.Contains(record.InstallationCode.ToUpper())
            )
        );
    }

    public IQueryable<AnalysisGroup> Filter(IQueryable<AnalysisGroup> query, AccessLevel level)
    {
        if (HasFullAccess(level))
            return query;
        var codes = InstallationCodes(level);
        return query.Where(group =>
            group.InspectionRecords.Any()
            && group.InspectionRecords.All(record =>
                codes.Contains(record.InstallationCode.ToUpper())
            )
            && group.Analyses.All(analysis =>
                analysis.InspectionRecords.Any()
                && analysis.InspectionRecords.All(record =>
                    codes.Contains(record.InstallationCode.ToUpper())
                )
            )
        );
    }

    public IQueryable<AnalysisRun> Filter(IQueryable<AnalysisRun> query, AccessLevel level)
    {
        if (HasFullAccess(level))
            return query;
        var codes = InstallationCodes(level);
        return query.Where(run =>
            run.Analysis.InspectionRecords.Any()
            && run.Analysis.InspectionRecords.All(record =>
                codes.Contains(record.InstallationCode.ToUpper())
            )
        );
    }

    public IQueryable<Workflow> Filter(IQueryable<Workflow> query, AccessLevel level)
    {
        if (HasFullAccess(level))
            return query;
        var codes = InstallationCodes(level);
        return query.Where(workflow =>
            workflow.AnalysisRun.Analysis.InspectionRecords.Any()
            && workflow.AnalysisRun.Analysis.InspectionRecords.All(record =>
                codes.Contains(record.InstallationCode.ToUpper())
            )
        );
    }

    public IQueryable<AnalysisRunFeedback> Filter(
        IQueryable<AnalysisRunFeedback> query,
        AccessLevel level
    )
    {
        if (HasFullAccess(level))
            return query;
        var codes = InstallationCodes(level);
        return query.Where(feedback =>
            feedback.AnalysisRun.Analysis.InspectionRecords.Any()
            && feedback.AnalysisRun.Analysis.InspectionRecords.All(record =>
                codes.Contains(record.InstallationCode.ToUpper())
            )
        );
    }

    public IQueryable<ReferencePolygonMetadata> Filter(
        IQueryable<ReferencePolygonMetadata> query,
        AccessLevel level
    )
    {
        if (HasFullAccess(level))
            return query;
        var codes = InstallationCodes(level);
        return query.Where(reference => codes.Contains(reference.InstallationCode.ToUpper()));
    }

    public static bool HasAccess(ClaimsPrincipal user, AccessLevel level) =>
        HasGlobalAccess(user) || GetInstallationCodes(user, level).Length > 0;

    private static bool HasGlobalAccess(ClaimsPrincipal user) => user.IsInRole("Role.Admin");

    private static string[] GetInstallationCodes(ClaimsPrincipal? user, AccessLevel level) =>
        user?.Identities.Where(identity => identity.IsAuthenticated)
            .SelectMany(identity => identity.FindAll(identity.RoleClaimType))
            .Select(claim => claim.Value.Split('.'))
            .Where(parts =>
                level != AccessLevel.Admin
                && parts.Length == 3
                && parts[0] == "Role"
                && parts[1] == "Observer"
                && parts[2].Length == 3
                && parts[2].All(char.IsAsciiLetterOrDigit)
            )
            .Select(parts => parts[2].ToUpperInvariant())
            .Distinct()
            .ToArray()
        ?? [];
}
