using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using api.Controllers.Models;
using api.Database.Context;
using Api.Test.Database;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Test.Controllers;

public class InspectionMeasurementControllerTests : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    private TestWebApplicationFactory<Program> _factory = null!;
    private SaraDbContext _context = null!;
    private HttpClient _client = null!;
    private string _measurementUrl = null!;

    public async ValueTask InitializeAsync()
    {
        (_container, string connectionString) =
            await TestSetupHelpers.ConfigurePostgreSqlDatabase();
        _factory = TestSetupHelpers.ConfigureWebApplicationFactory(connectionString);
        _context = TestSetupHelpers.ConfigurePostgreSqlContext(connectionString);
        var record = await new DatabaseUtilities(_context).NewInspectionRecord(
            blobName: "measurement.json",
            inspectionType: "CO2Measurement"
        );
        _measurementUrl = $"/api/inspection-record/id/{record.Id}/measurement";
        _client = TestSetupHelpers.ConfigureHttpClient(_factory);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _context.DisposeAsync();
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task GetMeasurement_ReturnsValueAndUnit()
    {
        _factory.BlobStorageService.BlobContent = """{"value":412.5,"unit":"ppm"}""";

        var measurement = await _client.GetFromJsonAsync<InspectionMeasurementDto>(
            _measurementUrl,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(new InspectionMeasurementDto(412.5, "ppm"), measurement);
    }

    [Fact]
    public async Task GetMeasurement_RejectsInvalidMeasurement()
    {
        _factory.BlobStorageService.BlobContent = """{"value":"invalid","unit":"ppm"}""";

        using var response = await _client.GetAsync(
            _measurementUrl,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
