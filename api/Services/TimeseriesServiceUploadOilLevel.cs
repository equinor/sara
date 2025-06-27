using System.Text;
using System.Text.Json;
using api.Controllers;
using api.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

public interface ITimeseriesServiceUploadOilLevel
{
    Task<bool> UploadCLODataAsync(ConstantLevelOilerDoneNotification notification);
}

public class TimeseriesServiceUploadOilLevel : ITimeseriesServiceUploadOilLevel
{
    private readonly IdaDbContext _dbContext;
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger<TimeseriesServiceUploadOilLevel> _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public TimeseriesServiceUploadOilLevel(
        IdaDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TimeseriesServiceUploadOilLevel> logger
    )
    {
        _dbContext = dbContext;
        _httpClient = httpClientFactory.CreateClient();
        _baseUrl =
            configuration["SARATimeseriesBaseUrl"]
            ?? throw new InvalidOperationException("SARATimeseriesBaseUrl is not configured.");
        _logger = logger;
    }

    public async Task<bool> UploadCLODataAsync(ConstantLevelOilerDoneNotification notification)
    {
        var plantData = await _dbContext.PlantData.FirstOrDefaultAsync(pd =>
            pd.InspectionId == notification.InspectionId
        );

        if (plantData == null)
        {
            _logger.LogWarning(
                "CLO upload skipped: no PlantData for InspectionId={InspectionId}",
                notification.InspectionId
            );
            return false;
        }

        var payload = new
        {
            name = "CLO_OilLevel",
            facility = plantData.InstallationCode,
            externalId = notification.InspectionId,
            description = "Oil level (0.0 empty–1.0 full) from Constant Level Oiler",
            unit = "fraction_full",
            assetId = plantData.InstallationCode,
            value = notification.OilLevel,
            timestamp = DateTime.UtcNow,
            metadata = new Dictionary<string, string>
            {
                ["inspectionId"] = notification.InspectionId,
                ["installation"] = plantData.InstallationCode,
            },
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var resp = await _httpClient.PostAsync(_baseUrl, content);
            if (resp.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "CLO oil level uploaded: InspectionId={InspectionId}, OilLevel={OilLevel}",
                    notification.InspectionId,
                    notification.OilLevel
                );
                return true;
            }
            else
            {
                var body = await resp.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Failed to upload CLO to Omnia Timeseries: {Status} {Body}",
                    resp.StatusCode,
                    body
                );
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Exception uploading CLO oil level for InspectionId={InspectionId}",
                notification.InspectionId
            );
            return false;
        }
    }
}
