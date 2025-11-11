using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using api.Database.Context;
using api.Database.Models;
using api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace api.Controllers.Tests
{
    public class AnalysisMappingControllerTest
    {
        private readonly Mock<ILogger<AnalysisMappingController>> _loggerMock;
        private readonly Mock<IAnalysisMappingService> _analysisMappingServiceMock;
        private readonly Mock<IPlantDataService> _plantDataServiceMock;
        private readonly SaraDbContext _dbContext;
        private readonly AnalysisMappingController _analysisMappingController;

        public AnalysisMappingControllerTest()
        {
            _loggerMock = new Mock<ILogger<AnalysisMappingController>>();
            _analysisMappingServiceMock = new Mock<IAnalysisMappingService>();
            _plantDataServiceMock = new Mock<IPlantDataService>();
            var options = new DbContextOptionsBuilder<SaraDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDatabase")
                .Options;
            _dbContext = new SaraDbContext(options);
            _analysisMappingController = new AnalysisMappingController(_loggerMock.Object, _analysisMappingServiceMock.Object, _plantDataServiceMock.Object, _dbContext);
        }

        [Fact]
        public async Task AddOrCreateAnalysisMapping_ReturnsStatusCode500_WhenExceptionIsThrown()
        {
            //Arrange
            string tagId = "dummy-tag-id";
            AnalysisType analysisType = AnalysisType.ConstantLevelOiler;
            string inspectionDescription = "Dummy description";

            _analysisMappingServiceMock
                .Setup(service => service.CreateAnalysisMapping(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<AnalysisType?>()
                ))
                .ThrowsAsync(new Exception("Test exception"));

            //Act
            var result = await _analysisMappingController.AddOrCreateAnalysisMapping(tagId, inspectionDescription, analysisType);

            //Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(500, statusCodeResult.StatusCode);
            Assert.Equal("An error occurred while creating the analysis mapping", statusCodeResult.Value);
        }

        [Fact]
        public async Task AddOrCreateAnalysisMapping_CreatesNewMapping_WhenNoneExists()
        {
            // Arrange
            string tagId = "test-tag";
            string inspectionDescription = "desc";
            AnalysisType analysisType = AnalysisType.ConstantLevelOiler;
            var newMapping = new AnalysisMapping(tagId, inspectionDescription)
            {
                AnalysesToBeRun = new List<AnalysisType> { analysisType }
            };

            _analysisMappingServiceMock
                .Setup(s => s.ReadByInspectionDescriptionAndTag(inspectionDescription, tagId))
                .ReturnsAsync((AnalysisMapping?)null);

            _analysisMappingServiceMock
                .Setup(s => s.CreateAnalysisMapping(tagId, inspectionDescription, analysisType))
                .ReturnsAsync(newMapping);

            _plantDataServiceMock
                .Setup(s => s.ReadByTagIdAndInspectionDescription(tagId, inspectionDescription))
                .ReturnsAsync((List<PlantData>)null!);

            // Act
            var result = await _analysisMappingController.AddOrCreateAnalysisMapping(tagId, inspectionDescription, analysisType);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(newMapping, okResult.Value);
            _analysisMappingServiceMock.Verify(s => s.CreateAnalysisMapping(tagId, inspectionDescription, analysisType), Times.Once);
        }
    }
}
