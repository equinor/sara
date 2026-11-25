using System.Text.Json;
using System.Text.RegularExpressions;
using api.Configurations;
using api.Controllers.Models;
using api.Database.Models;
using Azure;
using Microsoft.Extensions.Options;

namespace api.Services;

public record BenchmarkImageFile(byte[] Content, string ContentType);

public interface IBenchmarkService
{
    Task<IReadOnlyList<BenchmarkDatasetDto>> GetDatasetsAsync(
        CancellationToken cancellationToken = default
    );
    Task<IReadOnlyList<BenchmarkJobDto>> GetJobsAsync(
        CancellationToken cancellationToken = default
    );
    Task<BenchmarkJobDto> TriggerBenchmarkAsync(
        TriggerBenchmarkRequest request,
        CancellationToken cancellationToken = default
    );
    Task<IReadOnlyList<BenchmarkResultDto>> GetResultsAsync(
        string datasetId,
        string version,
        string split,
        CancellationToken cancellationToken = default
    );
    Task<BenchmarkRunDetailDto> GetRunDetailAsync(
        string datasetId,
        string version,
        string split,
        string runId,
        CancellationToken cancellationToken = default
    );
    Task<BenchmarkImageComparisonDto> GetImageComparisonAsync(
        string datasetId,
        string version,
        string split,
        string runId,
        int imageId,
        CancellationToken cancellationToken = default
    );
    Task<BenchmarkImageFile> GetDatasetImageAsync(
        string datasetId,
        string version,
        string split,
        int imageId,
        CancellationToken cancellationToken = default
    );
}

public partial class BenchmarkService(
    IArgoWorkflowClient argoWorkflowClient,
    IBlobStorageService blobStorageService,
    IBenchmarkIndexCache indexCache,
    IOptions<BenchmarkOptions> options,
    ILogger<BenchmarkService> logger
) : IBenchmarkService
{
    public const string BenchmarkLabel = "sara.equinor.com/benchmark";
    public const string DatasetLabel = "sara.equinor.com/benchmark-dataset";
    private const string BenchmarkSelector = BenchmarkLabel + "=true";
    private const int MaxConcurrentBlobReads = 8;
    private const long ParsedJsonMemoryMultiplier = 4;
    private static readonly IReadOnlyList<string> DefaultDevices = ["cpu", "gpu"];

    private readonly BenchmarkOptions _options = options.Value;

    public async Task<IReadOnlyList<BenchmarkDatasetDto>> GetDatasetsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var tasks = _options.Definitions.Select(def =>
            BuildDatasetDtoAsync(def, cancellationToken)
        );
        return await Task.WhenAll(tasks);
    }

    public async Task<IReadOnlyList<BenchmarkJobDto>> GetJobsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var workflows = await argoWorkflowClient.ListWorkflowsByLabelSelectorAsync(
            BenchmarkSelector,
            cancellationToken
        );

        return workflows
            .Select(MapWorkflowToJobDto)
            .OrderByDescending(j => j.CreatedAt ?? j.StartedAt ?? DateTime.MinValue)
            .ToList();
    }

    public async Task<BenchmarkJobDto> TriggerBenchmarkAsync(
        TriggerBenchmarkRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var definition = ResolveDefinition(request.DatasetId);
        ValidateSegment(request.Version, nameof(request.Version));
        ValidateSegment(request.Split, nameof(request.Split));

        var device = request.Device.Trim().ToLowerInvariant();
        if (!ResolveDevices(definition).Contains(device, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Unsupported device '{request.Device}' for dataset '{definition.Id}'."
            );
        }

        var runId = string.IsNullOrWhiteSpace(request.RunId) ? "" : request.RunId.Trim();
        if (!string.IsNullOrEmpty(runId))
        {
            ValidateSegment(runId, nameof(request.RunId));
            await EnsureRunIdIsAvailableAsync(
                definition,
                request.Version,
                request.Split,
                runId,
                cancellationToken
            );
        }

        var containerUrl = BuildContainerUrl(definition.Container);
        var workflow = new ArgoWorkflowResource
        {
            Metadata = new ArgoObjectMetadata
            {
                GenerateName = $"{definition.WorkflowTemplate}-",
                Labels = new Dictionary<string, string>
                {
                    [ArgoWorkflowClient.ManagedByLabel] = "sara",
                    [BenchmarkLabel] = "true",
                    [DatasetLabel] = definition.Id,
                },
            },
            Spec = new ArgoWorkflowSpec
            {
                WorkflowTemplateRef = new ArgoWorkflowTemplateRef
                {
                    Name = definition.WorkflowTemplate,
                },
                Arguments = new ArgoArguments
                {
                    Parameters =
                    [
                        new ArgoParameter { Name = "source", Value = containerUrl },
                        new ArgoParameter { Name = "destination", Value = containerUrl },
                        new ArgoParameter { Name = "version", Value = request.Version },
                        new ArgoParameter { Name = "split", Value = request.Split },
                        new ArgoParameter { Name = "run-id", Value = runId },
                        new ArgoParameter { Name = "device", Value = device },
                    ],
                },
            },
        };

        var created = await argoWorkflowClient.CreateWorkflowAsync(workflow, cancellationToken);
        return new BenchmarkJobDto(
            WorkflowName: created.Name,
            WorkflowUid: created.Uid,
            DatasetId: definition.Id,
            Version: request.Version,
            Split: request.Split,
            Device: device,
            RunId: string.IsNullOrEmpty(runId) ? created.Name : runId,
            Phase: "Pending",
            Message: null,
            CreatedAt: DateTime.UtcNow,
            StartedAt: null,
            FinishedAt: null
        );
    }

    public async Task<IReadOnlyList<BenchmarkResultDto>> GetResultsAsync(
        string datasetId,
        string version,
        string split,
        CancellationToken cancellationToken = default
    )
    {
        var definition = ResolveDefinition(datasetId);
        ValidateSegment(version, nameof(version));
        ValidateSegment(split, nameof(split));

        var resultsPrefix = $"{version}/benchmark-results/{split}/";
        var runIds = await blobStorageService.ListChildPrefixesAsync(
            _options.StorageAccount,
            definition.Container,
            resultsPrefix,
            cancellationToken
        );

        using var throttle = new SemaphoreSlim(MaxConcurrentBlobReads);
        var tasks = runIds.Select(runId =>
            ReadRunResultAsync(
                definition.Container,
                resultsPrefix,
                runId,
                throttle,
                cancellationToken
            )
        );
        var results = await Task.WhenAll(tasks);

        return results.OrderByDescending(r => r.RunId, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<BenchmarkRunDetailDto> GetRunDetailAsync(
        string datasetId,
        string version,
        string split,
        string runId,
        CancellationToken cancellationToken = default
    )
    {
        var definition = ResolveDefinition(datasetId);
        ValidateSegment(version, nameof(version));
        ValidateSegment(split, nameof(split));
        ValidateSegment(runId, nameof(runId));

        var prefix = $"{version}/benchmark-results/{split}/{runId}";
        BenchmarkRunMetadataDto? run;
        BenchmarkMetricsDto? metrics;
        try
        {
            run = await DownloadJsonAsync<BenchmarkRunMetadataDto>(
                definition.Container,
                $"{prefix}/run.json",
                cancellationToken
            );
            metrics = await DownloadJsonAsync<BenchmarkMetricsDto>(
                definition.Container,
                $"{prefix}/metrics.json",
                cancellationToken
            );
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new KeyNotFoundException($"Benchmark run '{runId}' was not found.");
        }
        catch (Exception ex) when (ex is RequestFailedException or JsonException)
        {
            return new BenchmarkRunDetailDto(runId, null, null, null, false, ex.Message);
        }

        var perImage = await TryGetPerImageAsync(definition.Container, prefix, cancellationToken);
        var hasPredictions = await blobStorageService.ExistsAsync(
            BuildBlobLocation(
                definition.Container,
                $"{prefix}/review/instances_segmentation_predictions.json"
            )
        );

        return new BenchmarkRunDetailDto(runId, run, metrics, perImage, hasPredictions, null);
    }

    public async Task<BenchmarkImageComparisonDto> GetImageComparisonAsync(
        string datasetId,
        string version,
        string split,
        string runId,
        int imageId,
        CancellationToken cancellationToken = default
    )
    {
        var definition = ResolveDefinition(datasetId);
        ValidateSegment(version, nameof(version));
        ValidateSegment(split, nameof(split));
        ValidateSegment(runId, nameof(runId));

        var gtIndex = await GetCachedCocoIndexAsync(
            definition.Container,
            $"{version}/dataset/annotations/instances_{split}.json",
            cancellationToken
        ) ?? throw new KeyNotFoundException("Ground-truth COCO annotations not found.");

        if (!gtIndex.Images.TryGetValue(imageId, out var image))
        {
            throw new KeyNotFoundException($"Image {imageId} not found in dataset split.");
        }

        var prefix = $"{version}/benchmark-results/{split}/{runId}";
        var predIndex = await GetCachedCocoIndexAsync(
            definition.Container,
            $"{prefix}/review/instances_segmentation_predictions.json",
            cancellationToken
        );
        var perImage = await TryGetPerImageAsync(definition.Container, prefix, cancellationToken);
        var summary = perImage?.FirstOrDefault(item => item.ImageId == imageId);

        return new BenchmarkImageComparisonDto(
            image.Id,
            image.FileName,
            image.Width,
            image.Height,
            summary,
            gtIndex.GetAnnotations(imageId),
            predIndex?.GetAnnotations(imageId) ?? []
        );
    }

    public async Task<BenchmarkImageFile> GetDatasetImageAsync(
        string datasetId,
        string version,
        string split,
        int imageId,
        CancellationToken cancellationToken = default
    )
    {
        var definition = ResolveDefinition(datasetId);
        ValidateSegment(version, nameof(version));
        ValidateSegment(split, nameof(split));

        var gtIndex = await GetCachedCocoIndexAsync(
            definition.Container,
            $"{version}/dataset/annotations/instances_{split}.json",
            cancellationToken
        ) ?? throw new KeyNotFoundException("Ground-truth COCO annotations not found.");

        if (!gtIndex.Images.TryGetValue(imageId, out var image))
        {
            throw new KeyNotFoundException($"Image {imageId} not found in dataset split.");
        }

        ValidateRelativeImagePath(image.FileName);
        var location = BuildBlobLocation(
            definition.Container,
            $"{version}/dataset/images/{split}/{image.FileName}"
        );
        try
        {
            using var stream = await blobStorageService.DownloadBlobAsync(location);
            return new BenchmarkImageFile(stream.ToArray(), ContentTypeForImage(image.FileName));
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new KeyNotFoundException($"Image blob '{image.FileName}' was not found.");
        }
    }

    private Task<IReadOnlyList<BenchmarkImageResultDto>?> TryGetPerImageAsync(
        string container,
        string runPrefix,
        CancellationToken cancellationToken
    )
    {
        var blobName = $"{runPrefix}/review/per_image.json";
        var cacheKey = $"benchmark:per-image:{_options.StorageAccount}/{container}/{blobName}";
        return indexCache.GetOrLoadAsync<IReadOnlyList<BenchmarkImageResultDto>>(
            cacheKey,
            async ct =>
            {
                try
                {
                    var (items, rawBytes) = await DownloadJsonWithSizeAsync<
                        List<BenchmarkImageResultDto>
                    >(container, blobName, ct);
                    return new CachedBenchmarkPayload<IReadOnlyList<BenchmarkImageResultDto>>(
                        items,
                        EstimateParsedJsonBytes(rawBytes)
                    );
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    return new CachedBenchmarkPayload<IReadOnlyList<BenchmarkImageResultDto>>(
                        null,
                        0
                    );
                }
            },
            cancellationToken
        );
    }

    private Task<BenchmarkCocoIndex?> GetCachedCocoIndexAsync(
        string container,
        string blobName,
        CancellationToken cancellationToken
    )
    {
        var cacheKey = $"benchmark:coco:{_options.StorageAccount}/{container}/{blobName}";
        return indexCache.GetOrLoadAsync<BenchmarkCocoIndex>(
            cacheKey,
            async ct =>
            {
                try
                {
                    using var stream = await blobStorageService.DownloadBlobAsync(
                        BuildBlobLocation(container, blobName)
                    );
                    var rawBytes = stream.Length;
                    var index = await BenchmarkCocoIndex.ParseAsync(stream, ct);
                    return new CachedBenchmarkPayload<BenchmarkCocoIndex>(
                        index,
                        EstimateParsedJsonBytes(rawBytes)
                    );
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    return new CachedBenchmarkPayload<BenchmarkCocoIndex>(null, 0);
                }
            },
            cancellationToken
        );
    }

    private async Task EnsureRunIdIsAvailableAsync(
        BenchmarkDefinition definition,
        string version,
        string split,
        string runId,
        CancellationToken cancellationToken
    )
    {
        var prefix = $"{version}/benchmark-results/{split}/{runId}";
        var runExists = await blobStorageService.ExistsAsync(
            BuildBlobLocation(definition.Container, $"{prefix}/run.json")
        );
        if (runExists)
        {
            throw new ArgumentException($"Benchmark run '{runId}' already exists.");
        }

        var workflows = await argoWorkflowClient.ListWorkflowsByLabelSelectorAsync(
            BenchmarkSelector,
            cancellationToken
        );
        var hasActiveRun = workflows
            .Select(MapWorkflowToJobDto)
            .Any(job =>
                IsActiveWorkflowPhase(job.Phase)
                && string.Equals(job.DatasetId, definition.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(job.Version, version, StringComparison.OrdinalIgnoreCase)
                && string.Equals(job.Split, split, StringComparison.OrdinalIgnoreCase)
                && string.Equals(job.RunId, runId, StringComparison.OrdinalIgnoreCase)
            );
        if (hasActiveRun)
        {
            throw new ArgumentException($"Benchmark run '{runId}' is already in progress.");
        }
    }

    private static bool IsActiveWorkflowPhase(string? phase) =>
        string.IsNullOrWhiteSpace(phase)
        || string.Equals(phase, "Pending", StringComparison.OrdinalIgnoreCase)
        || string.Equals(phase, "Running", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> ResolveDevices(BenchmarkDefinition definition) =>
        definition.Devices.Count > 0 ? definition.Devices : DefaultDevices;

    private static long EstimateParsedJsonBytes(long rawBytes)
    {
        if (rawBytes <= 0)
        {
            return 1;
        }

        return rawBytes > long.MaxValue / ParsedJsonMemoryMultiplier
            ? long.MaxValue
            : rawBytes * ParsedJsonMemoryMultiplier;
    }

    private BlobStorageLocation BuildBlobLocation(string container, string blobName) =>
        new()
        {
            StorageAccount = _options.StorageAccount,
            BlobContainer = container,
            BlobName = blobName,
        };

    private static void ValidateRelativeImagePath(string fileName)
    {
        if (
            string.IsNullOrWhiteSpace(fileName)
            || fileName.StartsWith('/')
            || fileName.Contains('\\')
            || fileName.Split('/').Any(part => part is "" or "." or "..")
        )
        {
            throw new ArgumentException($"Invalid COCO image path '{fileName}'.");
        }
    }

    private static string ContentTypeForImage(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream",
        };

    private async Task<BenchmarkDatasetDto> BuildDatasetDtoAsync(
        BenchmarkDefinition definition,
        CancellationToken cancellationToken
    )
    {
        var versions = new List<string>();
        try
        {
            var prefixes = await blobStorageService.ListChildPrefixesAsync(
                _options.StorageAccount,
                definition.Container,
                "",
                cancellationToken
            );

            var checks = await Task.WhenAll(
                prefixes.Select(async prefix =>
                {
                    var manifest = new BlobStorageLocation
                    {
                        StorageAccount = _options.StorageAccount,
                        BlobContainer = definition.Container,
                        BlobName = $"{prefix}/dataset/manifest.json",
                    };
                    var hasManifest = await blobStorageService.ExistsAsync(manifest);
                    return (prefix, hasManifest);
                })
            );

            versions.AddRange(
                checks
                    .Where(c => c.hasManifest)
                    .Select(c => c.prefix)
                    .OrderByDescending(v => v, StringComparer.OrdinalIgnoreCase)
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Failed to list versions for benchmark dataset '{DatasetId}' in container '{Container}'",
                definition.Id,
                definition.Container
            );
        }

        return new BenchmarkDatasetDto(
            definition.Id,
            definition.Label,
            definition.Container,
            definition.DefaultSplit,
            ResolveDevices(definition),
            versions
        );
    }

    private async Task<BenchmarkResultDto> ReadRunResultAsync(
        string container,
        string resultsPrefix,
        string runId,
        SemaphoreSlim throttle,
        CancellationToken cancellationToken
    )
    {
        await throttle.WaitAsync(cancellationToken);
        try
        {
            var run = await DownloadJsonAsync<BenchmarkRunMetadataDto>(
                container,
                $"{resultsPrefix}{runId}/run.json",
                cancellationToken
            );
            var metrics = await DownloadJsonAsync<BenchmarkMetricsDto>(
                container,
                $"{resultsPrefix}{runId}/metrics.json",
                cancellationToken
            );
            return new BenchmarkResultDto(runId, run, metrics, null);
        }
        catch (Exception ex) when (ex is RequestFailedException or JsonException)
        {
            logger.LogWarning(
                ex,
                "Failed to load benchmark result '{RunId}' from '{Container}/{Prefix}'",
                runId,
                container,
                resultsPrefix
            );
            return new BenchmarkResultDto(runId, null, null, ex.Message);
        }
        finally
        {
            throttle.Release();
        }
    }

    private async Task<T?> DownloadJsonAsync<T>(
        string container,
        string blobName,
        CancellationToken cancellationToken
    )
    {
        var (value, _) = await DownloadJsonWithSizeAsync<T>(container, blobName, cancellationToken);
        return value;
    }

    private async Task<(T? Value, long RawBytes)> DownloadJsonWithSizeAsync<T>(
        string container,
        string blobName,
        CancellationToken cancellationToken
    )
    {
        var location = BuildBlobLocation(container, blobName);
        using var stream = await blobStorageService.DownloadBlobAsync(location);
        var rawBytes = stream.Length;
        var value = await JsonSerializer.DeserializeAsync<T>(
            stream,
            cancellationToken: cancellationToken
        );
        return (value, rawBytes);
    }

    private BenchmarkDefinition ResolveDefinition(string datasetId) =>
        _options.Definitions.FirstOrDefault(d =>
            string.Equals(d.Id, datasetId, StringComparison.OrdinalIgnoreCase)
        ) ?? throw new ArgumentException($"Unknown benchmark dataset '{datasetId}'.");

    private string BuildContainerUrl(string container) =>
        $"https://{_options.StorageAccount}.blob.core.windows.net/{container}";

    private static BenchmarkJobDto MapWorkflowToJobDto(ArgoWorkflowResource workflow)
    {
        var parameters = (workflow.Spec?.Arguments?.Parameters ?? []).ToDictionary(
            p => p.Name,
            p => p.Value ?? "",
            StringComparer.OrdinalIgnoreCase
        );

        var name = workflow.Metadata.Name ?? "";
        var datasetId = workflow.Metadata.Labels.TryGetValue(DatasetLabel, out var labelDataset)
            ? labelDataset
            : "";
        var configuredRunId = parameters.GetValueOrDefault("run-id", "");

        return new BenchmarkJobDto(
            WorkflowName: name,
            WorkflowUid: workflow.Metadata.Uid,
            DatasetId: datasetId,
            Version: parameters.GetValueOrDefault("version", ""),
            Split: parameters.GetValueOrDefault("split", ""),
            Device: parameters.GetValueOrDefault("device", ""),
            RunId: string.IsNullOrEmpty(configuredRunId) ? name : configuredRunId,
            Phase: workflow.Status?.Phase ?? "Pending",
            Message: workflow.Status?.Message,
            CreatedAt: workflow.Metadata.CreationTimestamp,
            StartedAt: workflow.Status?.StartedAt,
            FinishedAt: workflow.Status?.FinishedAt
        );
    }

    private static void ValidateSegment(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value) || !PathSegmentRegex().IsMatch(value))
        {
            throw new ArgumentException($"Invalid {paramName}: '{value}'.");
        }
    }

    [GeneratedRegex("^[a-zA-Z0-9][a-zA-Z0-9._-]*$")]
    private static partial Regex PathSegmentRegex();
}
