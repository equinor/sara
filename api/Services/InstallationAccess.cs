using System.Security.Claims;
using api.Controllers.Models;
using api.Database.Models;

namespace api.Services;

public enum AccessMode
{
    Read,
    Write,
}

public class InstallationAccessException() : Exception("Installation access denied");

public class InstallationAccess(
    IHttpContextAccessor httpContextAccessor,
    ILogger<InstallationAccess> logger
)
{
    public const string ReadPolicy = "Installation.Read";
    public const string WritePolicy = "Installation.Write";

    // No HTTP context is trusted background processing; a missing auth header still has a context.
    public bool HasFullAccess(AccessMode mode) =>
        httpContextAccessor.HttpContext is null
        || HasGlobalAccess(httpContextAccessor.HttpContext.User, mode == AccessMode.Write);

    public string[] InstallationCodes(AccessMode mode) =>
        GetInstallationCodes(httpContextAccessor.HttpContext?.User, mode == AccessMode.Write);

    public void RequireAccess(string installationCode, AccessMode mode) =>
        Require(
            HasFullAccess(mode)
                || InstallationCodes(mode).Contains(installationCode.ToUpperInvariant())
        );

    public void RequireAccessToLookupResult(bool wasFound, AccessMode mode) =>
        Require(wasFound || HasFullAccess(mode));

    private void Require(bool permitted)
    {
        if (permitted)
            return;

        logger.LogWarning("Installation access denied");
        throw new InstallationAccessException();
    }

    public IQueryable<InspectionRecord> Filter(IQueryable<InspectionRecord> query, AccessMode mode)
    {
        if (HasFullAccess(mode))
            return query;
        var codes = InstallationCodes(mode);
        return query.Where(record => codes.Contains(record.InstallationCode.ToUpper()));
    }

    public IQueryable<Analysis> Filter(IQueryable<Analysis> query, AccessMode mode)
    {
        if (HasFullAccess(mode))
            return query;
        var codes = InstallationCodes(mode);
        return query.Where(analysis =>
            analysis.InspectionRecords.Any()
            && analysis.InspectionRecords.All(record =>
                codes.Contains(record.InstallationCode.ToUpper())
            )
        );
    }

    public IQueryable<AnalysisGroup> Filter(IQueryable<AnalysisGroup> query, AccessMode mode)
    {
        if (HasFullAccess(mode))
            return query;
        var codes = InstallationCodes(mode);
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

    public IQueryable<AnalysisRun> Filter(IQueryable<AnalysisRun> query, AccessMode mode)
    {
        if (HasFullAccess(mode))
            return query;
        var codes = InstallationCodes(mode);
        return query.Where(run =>
            run.Analysis.InspectionRecords.Any()
            && run.Analysis.InspectionRecords.All(record =>
                codes.Contains(record.InstallationCode.ToUpper())
            )
        );
    }

    public IQueryable<Workflow> Filter(IQueryable<Workflow> query, AccessMode mode)
    {
        if (HasFullAccess(mode))
            return query;
        var codes = InstallationCodes(mode);
        return query.Where(workflow =>
            workflow.AnalysisRun.Analysis.InspectionRecords.Any()
            && workflow.AnalysisRun.Analysis.InspectionRecords.All(record =>
                codes.Contains(record.InstallationCode.ToUpper())
            )
        );
    }

    public IQueryable<AnalysisRunFeedback> Filter(
        IQueryable<AnalysisRunFeedback> query,
        AccessMode mode
    )
    {
        if (HasFullAccess(mode))
            return query;
        var codes = InstallationCodes(mode);
        return query.Where(feedback =>
            feedback.AnalysisRun.Analysis.InspectionRecords.Any()
            && feedback.AnalysisRun.Analysis.InspectionRecords.All(record =>
                codes.Contains(record.InstallationCode.ToUpper())
            )
        );
    }

    public IQueryable<ReferencePolygonMetadata> Filter(
        IQueryable<ReferencePolygonMetadata> query,
        AccessMode mode
    )
    {
        if (HasFullAccess(mode))
            return query;
        var codes = InstallationCodes(mode);
        return query.Where(reference => codes.Contains(reference.InstallationCode.ToUpper()));
    }

    public static bool HasAccess(ClaimsPrincipal user, bool write) =>
        HasGlobalAccess(user, write) || GetInstallationCodes(user, write).Length > 0;

    private static bool HasGlobalAccess(ClaimsPrincipal user, bool write) =>
        user.IsInRole(Role.Admin) || (!write && user.IsInRole(Role.PlantDataRead));

    private static string[] GetInstallationCodes(ClaimsPrincipal? user, bool write) =>
        user?.Identities.Where(identity => identity.IsAuthenticated)
            .SelectMany(identity => identity.FindAll(identity.RoleClaimType))
            .Select(claim => claim.Value.Split('.'))
            .Where(parts =>
                parts.Length == 3
                && parts[0] == "Role"
                && (parts[1] == "User" || (!write && parts[1] == "ReadOnly"))
                && parts[2].Length == 3
                && parts[2].All(char.IsAsciiLetterOrDigit)
            )
            .Select(parts => parts[2].ToUpperInvariant())
            .Distinct()
            .ToArray()
        ?? [];
}
