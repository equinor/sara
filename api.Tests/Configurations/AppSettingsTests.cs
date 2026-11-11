using api.Configurations;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Test.Configurations;

public class AppSettingsTests
{
    [Theory]
    [InlineData("Video", "AnonymizeVideo")]
    [InlineData("ThermalVideo", "Passthrough")]
    [InlineData("AcousticMeasurement", "AnonymizeAcoustic")]
    public void Production_RoutesMp4ByInspectionType(string inspectionType, string expected)
    {
        var options = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Production.json")
            .Build()
            .GetSection(AnalysisOptions.SectionName)
            .Get<AnalysisOptions>()!;

        var analyses = options.DefaultAnalysisByInspectionTypeAndExtension[inspectionType][".mp4"];
        Assert.Equal(expected, Assert.Single(analyses));
    }
}
