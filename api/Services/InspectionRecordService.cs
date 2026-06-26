using api.Configurations;
using api.Database.Context;
using api.Database.Models;
using api.MQTT;
using api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace api.Services;

public interface IInspectionRecordService
{
    public Task<InspectionRecord> CreateFromMqttMessage(IsarInspectionResultMessage message);

    public Task<InspectionRecord> Create(InspectionRecord inspectionRecord);

    public Task<InspectionRecord> CreateAndTrigger(CreateInspectionRecordRequest request);

    public Task<InspectionRecord?> ReadById(Guid id);

    public Task<InspectionRecord?> ReadByInspectionId(string inspectionId);

    public Task<bool> ExistsByInspectionId(string inspectionId);

    public Task<PagedList<InspectionRecord>> GetInspectionRecords(
        InspectionRecordParameters parameters
    );

    public Task Delete(Guid id);

    public Task<InspectionRecord> AddAnalysis(Guid inspectionRecordId, string analysisName);
}

public class InspectionRecordParameters
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? InspectionId { get; set; }
    public string? Tag { get; set; }
    public string? InstallationCode { get; set; }
}

public class CreateInspectionRecordRequest
{
    public required string InspectionId { get; set; }
    public required string InstallationCode { get; set; }
    public required BlobStorageLocation BlobStorageLocation { get; set; }
    public string? InspectionType { get; set; }
    public string? Tag { get; set; }
    public string? InspectionDescription { get; set; }
    public string? RobotName { get; set; }
    public DateTime? Timestamp { get; set; }
    public List<string>? RequiredAnalysis { get; set; }
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
    ILogger<InspectionRecordService> logger
) : IInspectionRecordService
{
    private readonly AnalysisOptions _analysisOptions = analysisOptions.Value;

    public async Task<InspectionRecord> CreateFromMqttMessage(IsarInspectionResultMessage message)
    {
        var inspectionId = Sanitize.SanitizeUserInput(message.InspectionId);

        if (await ExistsByInspectionId(inspectionId))
        {
            throw new InvalidOperationException(
                $"Inspection record with inspection id {inspectionId} already exists"
            );
        }

        var inspectionRecord = new InspectionRecord
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
            Tag = Sanitize.SanitizeUserInput(message.TagID),
            InspectionDescription = Sanitize.SanitizeUserInput(message.InspectionDescription),
            RobotName = message.RobotName,
            Timestamp = message.Timestamp,
            RobotPose = message.RobotPose,
            TargetPosition = message.TargetPosition,
        };

        return await Create(inspectionRecord);
    }

    public async Task<InspectionRecord> Create(InspectionRecord inspectionRecord)
    {
        await context.InspectionRecords.AddAsync(inspectionRecord);
        await context.SaveChangesAsync();

        logger.LogInformation(
            "Created inspection record with InspectionId: {InspectionId}",
            inspectionRecord.InspectionId
        );

        return inspectionRecord;
    }

    public async Task<InspectionRecord?> ReadById(Guid id)
    {
        return await context
            .InspectionRecords.Include(ir => ir.Analyses)
                .ThenInclude(a => a.Runs)
                    .ThenInclude(r => r.Workflows)
            .FirstOrDefaultAsync(ir => ir.Id == id);
    }

    public async Task<InspectionRecord?> ReadByInspectionId(string inspectionId)
    {
        return await context
            .InspectionRecords.Include(ir => ir.Analyses)
                .ThenInclude(a => a.Runs)
                    .ThenInclude(r => r.Workflows)
            .FirstOrDefaultAsync(ir => ir.InspectionId == inspectionId);
    }

    public async Task<bool> ExistsByInspectionId(string inspectionId)
    {
        return await context.InspectionRecords.AnyAsync(ir => ir.InspectionId == inspectionId);
    }

    public async Task<InspectionRecord> CreateAndTrigger(CreateInspectionRecordRequest request)
    {
        var inspectionId = Sanitize.SanitizeUserInput(request.InspectionId);

        if (await ExistsByInspectionId(inspectionId))
        {
            throw new InvalidOperationException(
                $"Inspection record with inspection id {inspectionId} already exists"
            );
        }

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
            RobotName = request.RobotName,
            Timestamp = request.Timestamp,
        };

        var created = await Create(inspectionRecord);

        await analysisTriggerService.OnInspectionRecordCreated(
            new InspectionRecordCreatedEvent
            {
                InspectionRecordId = created.Id,
                RequiredAnalysis = request.RequiredAnalysis,
                AnalysisGroup = request.AnalysisGroup is null
                    ? null
                    : new IsarAnalysisGroupMessage
                    {
                        AnalysisGroupId = request.AnalysisGroup.AnalysisGroupId,
                        AnalysisGroupSize = request.AnalysisGroup.AnalysisGroupSize,
                        AnalysisGroupAnalyses = request.AnalysisGroup.AnalysisGroupAnalyses,
                    },
            }
        );

        return created;
    }

    public async Task Delete(Guid id)
    {
        var record = await context
            .InspectionRecords.Include(ir => ir.Analyses)
                .ThenInclude(a => a.Runs)
                    .ThenInclude(r => r.Workflows)
            .FirstOrDefaultAsync(ir => ir.Id == id);

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
        var query = context
            .InspectionRecords.Include(ir => ir.Analyses)
                .ThenInclude(a => a.Runs)
                    .ThenInclude(r => r.Workflows)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(parameters.InspectionId))
            query = query.Where(ir =>
                ir.InspectionId.ToLower().Contains(parameters.InspectionId.ToLower())
            );

        if (!string.IsNullOrWhiteSpace(parameters.Tag))
            query = query.Where(ir =>
                ir.Tag != null && ir.Tag.ToLower().Contains(parameters.Tag.ToLower())
            );

        if (!string.IsNullOrWhiteSpace(parameters.InstallationCode))
            query = query.Where(ir =>
                ir.InstallationCode.ToLower().Contains(parameters.InstallationCode.ToLower())
            );

        query = query.OrderByDescending(ir => ir.CreatedAt).ThenByDescending(ir => ir.Id);

        return await PagedList<InspectionRecord>.ToPagedListAsync(
            query,
            parameters.PageNumber,
            parameters.PageSize
        );
    }

    public async Task<InspectionRecord> AddAnalysis(Guid inspectionRecordId, string analysisName)
    {
        if (!_analysisOptions.Analyses.ContainsKey(analysisName))
        {
            throw new InvalidOperationException(
                $"Unknown analysis '{analysisName}'. Valid analyses are: "
                    + string.Join(", ", _analysisOptions.Analyses.Keys)
            );
        }

        var record =
            await ReadById(inspectionRecordId)
            ?? throw new KeyNotFoundException(
                $"Inspection record with id {inspectionRecordId} not found"
            );

        await analysisTriggerService.OnInspectionRecordCreated(
            new InspectionRecordCreatedEvent
            {
                InspectionRecordId = record.Id,
                RequiredAnalysis = [analysisName],
            }
        );

        return record;
    }
}
