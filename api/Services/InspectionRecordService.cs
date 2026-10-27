using api.Configurations;
using api.Database.Context;
using api.Database.Models;
using api.MQTT;
using api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace api.Services;

public interface IInspectionRecordService
{
    public Task<MqttInspectionRecordResult> CreateFromMqttMessage(
        IsarInspectionResultMessage message
    );

    public Task<MqttInspectionRecordResult> CreateFromMqttMessage(
        IsarInspectionValueMessage message
    );

    public Task<InspectionRecord> Create(InspectionRecord inspectionRecord);

    public Task<InspectionRecord> CreateAndTrigger(CreateInspectionRecordRequest request);

    public Task<InspectionRecord?> ReadById(Guid id);

    public Task<InspectionRecord?> ReadByInspectionId(string inspectionId);

    public Task<bool> ExistsByInspectionIdAndType(string inspectionId, string? inspectionType);

    public Task<PagedList<InspectionRecord>> GetInspectionRecords(
        InspectionRecordParameters parameters
    );

    public Task<PagedList<InspectionRecord>> GetInspectionRecordsBySourceAnalysisType(
        AnalysisTypeEnum sourceAnalysisType,
        int pageNumber,
        int pageSize
    );

    public Task Delete(Guid id);

    public Task<InspectionRecord> AddAnalysis(
        Guid inspectionRecordId,
        AnalysisTypeEnum analysisName
    );
}

public class InspectionRecordParameters
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public List<string>? InspectionIds { get; set; }
    public string? Tag { get; set; }
    public string? InstallationCode { get; set; }
    public DateTime? MinCreationTime { get; set; }
    public DateTime? MaxCreationTime { get; set; }
    public List<AnalysisTypeEnum>? AnalysisTypes { get; set; } = [];
}

public class CreateInspectionRecordRequest
{
    public required string InspectionId { get; set; }
    public required string InstallationCode { get; set; }
    public required BlobStorageLocation BlobStorageLocation { get; set; }
    public string? InspectionType { get; set; }
    public string? Tag { get; set; }
    public string? InspectionDescription { get; set; }
    public string? MissionName { get; set; }
    public string? RobotName { get; set; }
    public DateTime? Timestamp { get; set; }
    public List<AnalysisTypeEnum>? RequiredAnalysis { get; set; }
    public CreateInspectionRecordAnalysisGroup? AnalysisGroup { get; set; }
}

public class CreateInspectionRecordAnalysisGroup
{
    public required string AnalysisGroupId { get; set; }
    public required int AnalysisGroupSize { get; set; }
    public required List<string> AnalysisGroupAnalyses { get; set; }
}

public class InspectionRecordService(
    SaraDbContext context,
    IAnalysisTriggerService analysisTriggerService,
    IOptions<AnalysisOptions> analysisOptions,
    ILogger<InspectionRecordService> logger,
    InstallationAccess access
) : IInspectionRecordService
{
    private readonly AnalysisOptions _analysisOptions = analysisOptions.Value;

    public Task<MqttInspectionRecordResult> CreateFromMqttMessage(
        IsarInspectionResultMessage message
    ) =>
        CreateMqttRecordOnce(
            message.InspectionId,
            message.InspectionType,
            id => CreateMqttRecord(message, id)
        );

    public Task<MqttInspectionRecordResult> CreateFromMqttMessage(
        IsarInspectionValueMessage message
    ) =>
        CreateMqttRecordOnce(
            message.InspectionId,
            message.InspectionType,
            id => Create(NewInspectionValueRecord(message, id))
        );

    private async Task<MqttInspectionRecordResult> CreateMqttRecordOnce(
        string rawInspectionId,
        string? inspectionType,
        Func<string, Task<InspectionRecord>> create
    )
    {
        var inspectionId = Sanitize.SanitizeUserInput(rawInspectionId);
        // Retry the duplicate lookup too: a lost commit acknowledgement must not
        // retry insertion blindly or report the persisted record as newly created.
        var attempt = 0;
        return await context
            .Database.CreateExecutionStrategy()
            .ExecuteAsync(async () =>
            {
                // A transient failure may leave tracked entities from a rolled-back attempt.
                if (attempt++ > 0)
                    context.ChangeTracker.Clear();
                return await CreateFromMqttMessageAttempt(inspectionId, inspectionType, create);
            });
    }

    private async Task<MqttInspectionRecordResult> CreateFromMqttMessageAttempt(
        string inspectionId,
        string? inspectionType,
        Func<string, Task<InspectionRecord>> create
    )
    {
        var existing = await ReadMqttDuplicate(inspectionId, inspectionType);
        if (existing is not null)
        {
            return new MqttInspectionRecordResult(existing, true);
        }

        try
        {
            var created = await create(inspectionId);
            return new MqttInspectionRecordResult(created, false);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException
                    is PostgresException
                    {
                        SqlState: PostgresErrorCodes.UniqueViolation,
                        ConstraintName: "IX_InspectionRecords_InspectionId_InspectionType"
                            or "IX_AnalysisGroups_GroupId"
                    }
            )
        {
            context.ChangeTracker.Clear();
            existing = await ReadMqttDuplicate(inspectionId, inspectionType);
            if (existing is null)
                throw;

            return new MqttInspectionRecordResult(existing, true);
        }
    }

    private Task<InspectionRecord?> ReadMqttDuplicate(
        string inspectionId,
        string? inspectionType
    ) =>
        context
            .InspectionRecords.AsNoTracking()
            .FirstOrDefaultAsync(r =>
                r.InspectionId == inspectionId && r.InspectionType == inspectionType
            );

    private async Task<InspectionRecord> CreateMqttRecord(
        IsarInspectionResultMessage message,
        string inspectionId
    )
    {
        var analysisGroup =
            message.AnalysisGroup != null
                ? await GetOrCreateAnalysisGroup(
                    message.AnalysisGroup.AnalysisGroupId,
                    message.AnalysisGroup.AnalysisGroupSize
                )
                : null;

        var requiredAnalysis =
            message.RequiredAnalysis
            ?? GetDefaultAnalysis(
                message.InspectionType,
                Path.GetExtension(message.InspectionDataPath.BlobName)
            );
        var analyses = requiredAnalysis
            .Distinct()
            .Select(type =>
                analysisGroup?.Analyses.Find(a => a.AnalysisType == type)
                ?? new Analysis { AnalysisType = type, AnalysisGroup = analysisGroup }
            )
            .ToList();

        var inspectionRecord = new InspectionRecord
        {
            InspectionId = inspectionId,
            FlotillaMissionId = message.MissionId is null
                ? null
                : Sanitize.SanitizeUserInput(message.MissionId),
            InstallationCode = Sanitize.SanitizeUserInput(message.InstallationCode),
            BlobStorageLocation = new BlobStorageLocation
            {
                StorageAccount = message.InspectionDataPath.StorageAccount,
                BlobContainer = message.InspectionDataPath.BlobContainer,
                BlobName = message.InspectionDataPath.BlobName,
            },
            InspectionType = message.InspectionType,
            Tag = message.TagId is null ? null : Sanitize.SanitizeUserInput(message.TagId),
            InspectionDescription = Sanitize.SanitizeUserInput(message.InspectionDescription),
            MissionName = Sanitize.SanitizeUserInput(message.MissionName),
            RobotName = message.RobotName,
            Timestamp = message.Timestamp,
            RobotPose = message.RobotPose,
            TargetPosition = message.TargetPosition,
            Analyses = analyses,
            AnalysisGroup = analysisGroup,
            AnalysisGroupId = analysisGroup?.Id,
        };

        return await Create(inspectionRecord);
    }

    private InspectionRecord NewInspectionValueRecord(
        IsarInspectionValueMessage message,
        string inspectionId
    ) =>
        new()
        {
            InspectionId = inspectionId,
            InstallationCode = Sanitize.SanitizeUserInput(message.InstallationCode),
            BlobStorageLocation = new BlobStorageLocation
            {
                StorageAccount = message.InspectionDataPath.StorageAccount,
                BlobContainer = message.InspectionDataPath.BlobContainer,
                BlobName = message.InspectionDataPath.BlobName,
            },
            InspectionType = message.InspectionType,
            Tag = message.TagID is null ? null : Sanitize.SanitizeUserInput(message.TagID),
            InspectionDescription = message.InspectionDescription is null
                ? null
                : Sanitize.SanitizeUserInput(message.InspectionDescription),
            RobotName = message.RobotName,
            Timestamp = message.Timestamp,
            RobotPose = message.RobotPose,
            TargetPosition = message.TargetPosition,
            Analyses = GetDefaultAnalysis(message.InspectionType, ".json")
                .Distinct()
                .Select(type => new Analysis { AnalysisType = type })
                .ToList(),
        };

    /// <summary>
    /// Resolves the configured default analyses for an inspection record that carries no
    /// explicit RequiredAnalysis. A DefaultAnalysisByInspectionTypeAndExtension match on
    /// inspection type plus file extension wins over the type-only
    /// DefaultAnalysisByInspectionType map. Both lookups are case-insensitive.
    /// </summary>
    private IReadOnlyList<string> GetDefaultAnalysis(string inspectionType, string? extension)
    {
        if (!string.IsNullOrEmpty(extension))
        {
            var byExtension = _analysisOptions
                .DefaultAnalysisByInspectionTypeAndExtension.FirstOrDefault(entry =>
                    string.Equals(entry.Key, inspectionType, StringComparison.OrdinalIgnoreCase)
                )
                .Value?.FirstOrDefault(entry =>
                    string.Equals(entry.Key, extension, StringComparison.OrdinalIgnoreCase)
                )
                .Value;

            if (byExtension is { Count: > 0 })
                return byExtension;
        }

        return _analysisOptions
                .DefaultAnalysisByInspectionType.FirstOrDefault(entry =>
                    string.Equals(entry.Key, inspectionType, StringComparison.OrdinalIgnoreCase)
                )
                .Value
            ?? [];
    }

    private async Task<AnalysisGroup> GetOrCreateAnalysisGroup(
        string analysisGroupId,
        int analysisGroupSize
    )
    {
        var existing = await access
            .Filter(context.AnalysisGroups, AccessLevel.Admin)
            .Include(g => g.Analyses)
            .FirstOrDefaultAsync(g => g.GroupId == analysisGroupId);

        if (existing is not null)
        {
            return existing;
        }

        var timeoutMinutes = analysisOptions.Value.AnalysisGroupTimeoutMinutes;
        var group = new AnalysisGroup
        {
            GroupId = analysisGroupId,
            ExpectedSize = analysisGroupSize,
            TimeoutAt = DateTime.UtcNow.AddMinutes(timeoutMinutes),
        };

        await context.AnalysisGroups.AddAsync(group);

        return group;
    }

    public async Task<InspectionRecord> Create(InspectionRecord inspectionRecord)
    {
        access.RequireAccess(inspectionRecord.InstallationCode, AccessLevel.Admin);
        await context.InspectionRecords.AddAsync(inspectionRecord);
        var newGroup = inspectionRecord.AnalysisGroup;
        var isNewGroup = newGroup is not null && context.Entry(newGroup).State == EntityState.Added;

        // Persist the entire graph in one implicit transaction.
        await context.SaveChangesAsync();

        if (isNewGroup)
            logger.LogInformation(
                "Created analysis group {GroupId} expecting {ExpectedSize} records, timeout at {TimeoutAt}",
                newGroup!.Id,
                newGroup.ExpectedSize,
                newGroup.TimeoutAt
            );

        logger.LogInformation(
            "Created inspection record with InspectionId: {InspectionId}",
            inspectionRecord.InspectionId
        );

        foreach (var analysis in inspectionRecord.Analyses)
        {
            if (analysis.AnalysisGroup != null)
                context.Entry(analysis.AnalysisGroup).State = EntityState.Detached;
            context.Entry(analysis).State = EntityState.Detached;
        }
        context.Entry(inspectionRecord).State = EntityState.Detached;

        return inspectionRecord;
    }

    private IQueryable<InspectionRecord> GetRecords(AccessLevel level)
    {
        var analysisIds = access.Filter(context.Analyses, level).Select(a => a.Id);
        var query = access.Filter(context.InspectionRecords, level);
        if (level == AccessLevel.Admin)
            query = query.Where(record => record.Analyses.All(a => analysisIds.Contains(a.Id)));
        else
            query = query.AsNoTracking();

        return query
            .Include(ir => ir.Analyses.Where(a => analysisIds.Contains(a.Id)))
                .ThenInclude(a => a.Runs)
                    .ThenInclude(r => r.Workflows)
            .Include(ir => ir.Analyses.Where(a => analysisIds.Contains(a.Id)))
                .ThenInclude(a => a.Runs)
                    .ThenInclude(r => r.Feedback);
    }

    public async Task<InspectionRecord?> ReadById(Guid id)
    {
        var record = await GetRecords(AccessLevel.Observer).FirstOrDefaultAsync(ir => ir.Id == id);
        access.RequireAccessToLookupResult(wasFound: record is not null, AccessLevel.Observer);
        return record;
    }

    public async Task<InspectionRecord?> ReadByInspectionId(string inspectionId)
    {
        var record = await GetRecords(AccessLevel.Observer)
            .FirstOrDefaultAsync(ir => ir.InspectionId == inspectionId);
        access.RequireAccessToLookupResult(wasFound: record is not null, AccessLevel.Observer);
        return record;
    }

    public async Task<bool> ExistsByInspectionIdAndType(string inspectionId, string? inspectionType)
    {
        return await access
            .Filter(context.InspectionRecords, AccessLevel.Observer)
            .AnyAsync(ir => ir.InspectionId == inspectionId && ir.InspectionType == inspectionType);
    }

    public async Task<InspectionRecord> CreateAndTrigger(CreateInspectionRecordRequest request)
    {
        access.RequireAccess(request.InstallationCode, AccessLevel.Admin);
        var inspectionId = Sanitize.SanitizeUserInput(request.InspectionId);

        if (await ExistsByInspectionIdAndType(inspectionId, request.InspectionType))
        {
            throw new InvalidOperationException(
                $"Inspection record with inspection id {inspectionId} and inspection type {request.InspectionType} already exists"
            );
        }

        var analysisGroup =
            request.AnalysisGroup != null
                ? await GetOrCreateAnalysisGroup(
                    request.AnalysisGroup.AnalysisGroupId,
                    request.AnalysisGroup.AnalysisGroupSize
                )
                : null;

        var analysisTypes = request.RequiredAnalysis?.Select((a) => a.ToString()).ToList();

        var analyses =
            analysisTypes != null
                ? analysisTypes
                    .Select(
                        (r) =>
                            analysisGroup?.Analyses.Find((a) => a.AnalysisType == r)
                            ?? new Analysis { AnalysisType = r, AnalysisGroup = analysisGroup }
                    )
                    .ToList()
                : [];

        var inspectionRecord = new InspectionRecord
        {
            InspectionId = inspectionId,
            InstallationCode = Sanitize.SanitizeUserInput(request.InstallationCode),
            BlobStorageLocation = request.BlobStorageLocation,
            InspectionType = request.InspectionType,
            Tag = request.Tag is null ? null : Sanitize.SanitizeUserInput(request.Tag),
            InspectionDescription = request.InspectionDescription is null
                ? null
                : Sanitize.SanitizeUserInput(request.InspectionDescription),
            MissionName = request.MissionName is null
                ? null
                : Sanitize.SanitizeUserInput(request.MissionName),
            RobotName = request.RobotName,
            Timestamp = request.Timestamp,
            Analyses = analyses,
            AnalysisGroup = analysisGroup,
            AnalysisGroupId = analysisGroup?.Id,
        };

        var created = await Create(inspectionRecord);

        await analysisTriggerService.OnInspectionRecordCreated(created);

        return created;
    }

    public async Task<PagedList<InspectionRecord>> GetInspectionRecordsBySourceAnalysisType(
        AnalysisTypeEnum sourceAnalysisType,
        int pageNumber,
        int pageSize
    )
    {
        var analysisTypeName = sourceAnalysisType.ToString();
        var analysisIds = access.Filter(context.Analyses, AccessLevel.Observer).Select(a => a.Id);
        var query = GetRecords(AccessLevel.Observer)
            .Where(ir =>
                ir.Analyses.Any(a =>
                    analysisIds.Contains(a.Id) && a.AnalysisType == analysisTypeName
                )
                && ir.Analyses.Any(a =>
                    analysisIds.Contains(a.Id)
                    && a.Runs.Any(r =>
                        r.Workflows.Any(w =>
                            w.WorkflowType == "anonymizer" && w.Status == WorkflowStatus.Succeeded
                        )
                    )
                )
            )
            .OrderByDescending(ir => ir.CreatedAt)
            .ThenByDescending(ir => ir.Id);

        return await PagedList<InspectionRecord>.ToPagedListAsync(query, pageNumber, pageSize);
    }

    public async Task Delete(Guid id)
    {
        var record = await GetRecords(AccessLevel.Admin).FirstOrDefaultAsync(ir => ir.Id == id);

        access.RequireAccessToLookupResult(wasFound: record is not null, AccessLevel.Admin);
        if (record is null)
        {
            throw new KeyNotFoundException($"Inspection record with id {id} not found");
        }

        // Detach analyses that have other inspection records; cascade-delete those
        // that belong only to this record (along with their runs and workflows).
        var orphanedAnalyses = new List<Analysis>();
        foreach (var analysis in record.Analyses.ToList())
        {
            await context.Entry(analysis).Collection(a => a.InspectionRecords).LoadAsync();
            analysis.InspectionRecords.Remove(record);
            if (analysis.InspectionRecords.Count == 0)
            {
                orphanedAnalyses.Add(analysis);
            }
        }

        foreach (var analysis in orphanedAnalyses)
        {
            foreach (var run in analysis.Runs)
            {
                context.Workflows.RemoveRange(run.Workflows);
            }
            context.AnalysisRuns.RemoveRange(analysis.Runs);
            context.Analyses.Remove(analysis);
        }

        context.InspectionRecords.Remove(record);
        await context.SaveChangesAsync();

        logger.LogInformation("Deleted inspection record {Id}", id);
    }

    public async Task<PagedList<InspectionRecord>> GetInspectionRecords(
        InspectionRecordParameters parameters
    )
    {
        var query = GetRecords(AccessLevel.Observer);

        if (parameters.InspectionIds != null && parameters.InspectionIds.Count > 0)
            query = query.Where(ir =>
                parameters
                    .InspectionIds.Select((i) => i.ToLower())
                    .Contains(ir.InspectionId.ToLower())
            );

        if (!string.IsNullOrWhiteSpace(parameters.Tag))
            query = query.Where(ir =>
                ir.Tag != null && ir.Tag.ToLower().Contains(parameters.Tag.ToLower())
            );

        if (!string.IsNullOrWhiteSpace(parameters.InstallationCode))
            query = query.Where(ir =>
                ir.InstallationCode.ToLower().Contains(parameters.InstallationCode.ToLower())
            );

        if (parameters.MinCreationTime != null)
            query = query.Where(ir => ir.CreatedAt > parameters.MinCreationTime);

        if (parameters.MaxCreationTime != null)
            query = query.Where(ir => ir.CreatedAt < parameters.MaxCreationTime);

        if (parameters.AnalysisTypes != null && parameters.AnalysisTypes.Count > 0)
        {
            List<string> analysisTypeStrings =
            [
                .. parameters.AnalysisTypes.Select((a) => a.ToString()),
            ];
            var analysisIds = access
                .Filter(context.Analyses, AccessLevel.Observer)
                .Select(a => a.Id);
            query = query.Where(ir =>
                ir.Analyses.Any(a =>
                    analysisIds.Contains(a.Id) && analysisTypeStrings.Contains(a.AnalysisType)
                )
            );
        }

        query = query.OrderByDescending(ir => ir.CreatedAt).ThenByDescending(ir => ir.Id);

        return await PagedList<InspectionRecord>.ToPagedListAsync(
            query,
            parameters.PageNumber,
            parameters.PageSize
        );
    }

    public async Task<InspectionRecord> AddAnalysis(
        Guid inspectionRecordId,
        AnalysisTypeEnum analysisName
    )
    {
        var analysisType = analysisName.ToString();
        if (!_analysisOptions.Analyses.ContainsKey(analysisType))
        {
            throw new InvalidOperationException(
                $"Unknown analysis '{analysisType}'. Valid analyses are: "
                    + string.Join(", ", _analysisOptions.Analyses.Keys)
            );
        }

        var record = await GetRecords(AccessLevel.Admin)
            .FirstOrDefaultAsync(ir => ir.Id == inspectionRecordId);
        access.RequireAccessToLookupResult(wasFound: record is not null, AccessLevel.Admin);
        if (record is null)
            throw new KeyNotFoundException(
                $"Inspection record with id {inspectionRecordId} not found"
            );

        await analysisTriggerService.OnInspectionRecordCreated(record);

        return record;
    }
}
