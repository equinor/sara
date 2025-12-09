using System;
using System.Threading.Tasks;
using api.Database.Context;
using api.Database.Models;
using api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Test.Services;

public class AnalysisMappingServiceTests
{
    private static SaraDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<SaraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new SaraDbContext(options);
    }

    [Fact]
    public async Task ReadByTagAndInspectionDescription_ExistingMapping_ReturnsMapping()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AnalysisMappingService>>();
        var service = new AnalysisMappingService(context, loggerMock.Object);

        // Create a mapping first
        await service.CreateAnalysisMapping(
            "TAG-001",
            "Oil Level",
            AnalysisType.ConstantLevelOiler
        );

        // Act
        var result = await service.ReadByTagAndInspectionDescription("TAG-001", "Oil Level");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TAG-001", result.Tag);
        Assert.Equal("Oil Level", result.InspectionDescription);
        Assert.Equal([AnalysisType.ConstantLevelOiler], result.AnalysesToBeRun);
    }

    [Fact]
    public async Task CreateAnalysisMapping_ValidInput_CreatesMapping()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AnalysisMappingService>>();
        var service = new AnalysisMappingService(context, loggerMock.Object);

        // Act
        var result = await service.CreateAnalysisMapping(
            "TAG-001",
            "Oil Level",
            AnalysisType.ConstantLevelOiler
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TAG-001", result.Tag);
        Assert.Equal("Oil Level", result.InspectionDescription);
        Assert.Contains(AnalysisType.ConstantLevelOiler, result.AnalysesToBeRun);

        // Verify it was saved to database
        var savedMapping = await context.AnalysisMapping.FirstOrDefaultAsync(m =>
            m.Tag == "TAG-001"
        );
        Assert.NotNull(savedMapping);
    }

    [Fact]
    public async Task CreateAnalysisMapping_EmptyTagId_ThrowsArgumentException()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AnalysisMappingService>>();
        var service = new AnalysisMappingService(context, loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateAnalysisMapping("", "Oil Level", AnalysisType.ConstantLevelOiler)
        );
    }

    [Fact]
    public async Task AddAnalysisTypeToMapping_NewType_AddsSuccessfully()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AnalysisMappingService>>();
        var service = new AnalysisMappingService(context, loggerMock.Object);

        var mapping = await service.CreateAnalysisMapping(
            "TAG-001",
            "Break Detection",
            AnalysisType.Fencilla
        );

        // Act
        var result = await service.AddAnalysisTypeToMapping(
            mapping,
            AnalysisType.ConstantLevelOiler
        );

        // Assert
        Assert.Contains(AnalysisType.Fencilla, result.AnalysesToBeRun);
        Assert.Contains(AnalysisType.ConstantLevelOiler, result.AnalysesToBeRun);
    }

    [Fact]
    public async Task AddAnalysisTypeToMapping_DuplicateType_ThrowsArgumentException()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AnalysisMappingService>>();
        var service = new AnalysisMappingService(context, loggerMock.Object);

        var mapping = await service.CreateAnalysisMapping(
            "TAG-001",
            "Oil Level",
            AnalysisType.ConstantLevelOiler
        );

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.AddAnalysisTypeToMapping(mapping, AnalysisType.ConstantLevelOiler)
        );
    }

    [Fact]
    public async Task GetAnalysesToBeRun_MappingExists_ReturnsMapping()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AnalysisMappingService>>();
        var service = new AnalysisMappingService(context, loggerMock.Object);

        await service.CreateAnalysisMapping(
            "TAG-001",
            "Oil Level",
            AnalysisType.ConstantLevelOiler
        );

        // Act
        var result = await service.GetAnalysesToBeRun("TAG-001", "Oil Level");

        // Assert
        Assert.Contains(AnalysisType.ConstantLevelOiler, result);
    }

    [Fact]
    public async Task GetAnalysesToBeRun_NoMapping_ReturnsEmptyList()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AnalysisMappingService>>();
        var service = new AnalysisMappingService(context, loggerMock.Object);

        // Act
        var result = await service.GetAnalysesToBeRun("TAG-999", "Non-existent");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task RemoveAnalysisTypeFromMapping_ExistingType_RemovesSuccessfully()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AnalysisMappingService>>();
        var service = new AnalysisMappingService(context, loggerMock.Object);

        var mapping = await service.CreateAnalysisMapping(
            "TAG-001",
            "Oil Level",
            AnalysisType.ConstantLevelOiler
        );
        await service.AddAnalysisTypeToMapping(mapping, AnalysisType.Fencilla);

        // Act
        var result = await service.RemoveAnalysisTypeFromMapping(mapping.Id, AnalysisType.Fencilla);

        // Assert
        Assert.DoesNotContain(AnalysisType.Fencilla, result.AnalysesToBeRun);
        Assert.Contains(AnalysisType.ConstantLevelOiler, result.AnalysesToBeRun);
    }

    [Fact]
    public async Task AddOrCreateAnalysisMapping_ValidInput_AddsAnalysisMapping()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AnalysisMappingService>>();
        var analysisMappingService = new AnalysisMappingService(context, loggerMock.Object);

        // Act
        await analysisMappingService.AddOrCreateAnalysisMapping(
            "dummyTAG-001",
            "Oil Level",
            AnalysisType.ConstantLevelOiler
        );

        // Assert
        var mapping = await analysisMappingService.ReadByTagAndInspectionDescription(
            "dummyTAG-001",
            "Oil Level"
        );
        Assert.NotNull(mapping);
        Assert.Contains(AnalysisType.ConstantLevelOiler, mapping.AnalysesToBeRun);
        Assert.Single(mapping.AnalysesToBeRun);
    }

    [Fact]
    public async Task AddOrCreateAnalysisMapping_ValidInput_UpdatesExistingAnalysisMapping()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AnalysisMappingService>>();
        var analysisMappingService = new AnalysisMappingService(context, loggerMock.Object);

        // Act
        await analysisMappingService.AddOrCreateAnalysisMapping(
            "dummyTAG-001",
            "Double inspection",
            AnalysisType.ConstantLevelOiler
        );

        await analysisMappingService.AddOrCreateAnalysisMapping(
            "dummyTAG-001",
            "Double inspection",
            AnalysisType.Fencilla
        );

        // Assert
        var mapping = await analysisMappingService.ReadByTagAndInspectionDescription(
            "dummyTAG-001",
            "Double inspection"
        );
        Assert.NotNull(mapping);
        Assert.Equal(2, mapping.AnalysesToBeRun.Count);
        Assert.Contains(AnalysisType.ConstantLevelOiler, mapping.AnalysesToBeRun);
        Assert.Contains(AnalysisType.Fencilla, mapping.AnalysesToBeRun);
    }
}
