using System;
using System.Threading.Tasks;
using api.Database.Context;
using api.Database.Models;
using api.Services;
using api.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace api.Controllers.Tests
{
    public class PlantDataControllerTest
    {
        private readonly PlantDataService _plantDataService;
        private readonly AnalysisMappingService _analysisMappingService;
        private readonly PlantDataController _plantDataController;

        private static SaraDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<SaraDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new SaraDbContext(options);
        }

        public PlantDataControllerTest()
        {
            var context = CreateInMemoryContext();
            var loggerServiceMock = new Mock<ILogger<PlantDataService>>();
            var loggerControllerMock = new Mock<ILogger<PlantDataController>>();
            var loggerAnalysisMappingServiceMock = new Mock<ILogger<AnalysisMappingService>>();
            var blobServiceMock = new Mock<IBlobService>();

            _analysisMappingService = new AnalysisMappingService(
                context,
                loggerAnalysisMappingServiceMock.Object
            );

            _plantDataService = new PlantDataService(
                context,
                _analysisMappingService,
                blobServiceMock.Object,
                loggerServiceMock.Object
            );

            _plantDataController = new PlantDataController(
                loggerControllerMock.Object,
                _plantDataService
            );
        }

        [Fact]
        public async Task CreatePlantData_ReturnsCreated_WhenPlantDataCreated()
        {
            // Arrange
            var request = new PlantDataRequest
            {
                InspectionId = "dummyInspectionId",
                InstallationCode = "dummyInstallationCode",
                TagId = "dummyTagId",
                InspectionDescription = "dummyInspectionDescription",
                RawDataBlobStorageLocation = new BlobStorageLocation
                {
                    StorageAccount = "dummy",
                    BlobContainer = "dummy",
                    BlobName = "dummy.jpg",
                },
            };
            var expectedPlantData = new PlantData
            {
                InspectionId = "dummyInspectionId",
                InstallationCode = "dummyInstallationCode",
                Tag = "dummyTagId",
                InspectionDescription = "dummyInspectionDescription",
                Anonymization = new Anonymization
                {
                    DestinationBlobStorageLocation = new BlobStorageLocation
                    {
                        StorageAccount = "dummyRawStorageAccount",
                        BlobContainer = "dummyRawBlobContainer",
                        BlobName = "dummyRawBlobName",
                    },
                    SourceBlobStorageLocation = new BlobStorageLocation
                    {
                        StorageAccount = "dummyAnonStorageAccount",
                        BlobContainer = "dummyAnonBlobContainer",
                        BlobName = "dummyBlobName",
                    },
                },
            };

            // Act
            var result = await _plantDataController.CreatePlantData(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(nameof(_plantDataController.GetPlantDataById), createdResult.ActionName);

            var createdPlantData = createdResult.Value as PlantData;
            Assert.NotNull(createdPlantData);
            Assert.Equal(expectedPlantData.InspectionId, createdPlantData.InspectionId);
            Assert.Equal(expectedPlantData.InstallationCode, createdPlantData.InstallationCode);
            Assert.Equal(expectedPlantData.Tag, createdPlantData.Tag);
            Assert.Equal(
                expectedPlantData.InspectionDescription,
                createdPlantData.InspectionDescription
            );
            Assert.NotNull(createdPlantData.Anonymization);
            Assert.Null(createdPlantData.CLOEAnalysis);
            Assert.Null(createdPlantData.FencillaAnalysis);
        }

        [Fact]
        public async Task CreatePlantData_AddsAnalysesFromMapping_WhenMappingExists()
        {
            // Arrange
            var request = new PlantDataRequest
            {
                InspectionId = "dummyInspectionId",
                InstallationCode = "dummyInstallationCode",
                TagId = "TAG-001",
                InspectionDescription = "Oil Level",
                RawDataBlobStorageLocation = new BlobStorageLocation
                {
                    StorageAccount = "dummy",
                    BlobContainer = "dummy",
                    BlobName = "dummy.jpg",
                },
            };

            await _analysisMappingService.CreateAnalysisMapping(
                "TAG-001",
                "Oil Level",
                AnalysisType.ConstantLevelOiler
            );

            // Act
            var result = await _plantDataController.CreatePlantData(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            var returnedPlantData = Assert.IsType<PlantData>(createdResult.Value);

            Assert.NotNull(returnedPlantData.CLOEAnalysis);
            Assert.Null(returnedPlantData.FencillaAnalysis);
        }

        [Fact]
        public async Task CreatePlantData_ReturnsBadRequest_WhenRequiredFieldsAreEmpty()
        {
            // Arrange
            var request = new PlantDataRequest
            {
                InspectionId = "",
                InstallationCode = "",
                TagId = "",
                InspectionDescription = "",
                RawDataBlobStorageLocation = new BlobStorageLocation
                {
                    StorageAccount = "dummy",
                    BlobContainer = "dummy",
                    BlobName = "dummy.jpg",
                },
            };

            // Act
            IActionResult result = await _plantDataController.CreatePlantData(request);

            // Assert
            var statusResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, statusResult.StatusCode);
        }
    }
}
