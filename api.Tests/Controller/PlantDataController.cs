using System;
using System.Threading.Tasks;
using api.Utilities;
using api.Database.Models;
using api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Generic;
using Xunit;

namespace api.Controllers.Tests
{
    public class PlantDataControllerTest
    {
        private readonly Mock<ILogger<PlantDataController>> _loggerMock;
        private readonly Mock<IPlantDataService> _serviceMock;
        private readonly Mock<IAnalysisMappingService> _analysisMappingServiceMock;
        private readonly PlantDataController _controller;

        public PlantDataControllerTest()
        {
            _loggerMock = new Mock<ILogger<PlantDataController>>();
            _serviceMock = new Mock<IPlantDataService>();
            _analysisMappingServiceMock = new Mock<IAnalysisMappingService>();
            _controller = new PlantDataController(_loggerMock.Object, _serviceMock.Object, _analysisMappingServiceMock.Object);
        }

        [Fact]
        public async Task CreatePlantDataEntry_ReturnsBadRequest_WhenInspectionIdMissing()
        {
            var request = new PlantDataRequest
            {
                InspectionId = "",
                InstallationCode = "dummy",
                RawDataBlobStorageLocation = new BlobStorageLocation(),
                AnonymizedBlobStorageLocation = new BlobStorageLocation(),
                VisualizedBlobStorageLocation = new BlobStorageLocation(),
                TagId = "dummy",
                InspectionDescription = "dummy",
                AnalysisToBeRun = new List<AnalysisType>()
            };

            var result = await _controller.CreatePlantDataEntry(request);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task CreatePlantDataEntry_ReturnsBadRequest_WhenInstallationCodeMissing()
        {
            var request = new PlantDataRequest
            {
                InspectionId = "dummy",
                InstallationCode = "",
                RawDataBlobStorageLocation = new BlobStorageLocation(),
                AnonymizedBlobStorageLocation = new BlobStorageLocation(),
                VisualizedBlobStorageLocation = new BlobStorageLocation(),
                TagId = "dummy",
                InspectionDescription = "dummy",
                AnalysisToBeRun = new List<AnalysisType>()
            };
            var result = await _controller.CreatePlantDataEntry(request);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task CreatePlantDataEntry_ReturnsCreated_WhenPlantDataCreated()
        {
            var request = new PlantDataRequest
            {
                InspectionId = "dummy",
                InstallationCode = "dummy",
                RawDataBlobStorageLocation = new BlobStorageLocation { StorageAccount = "dummy", BlobContainer = "dummy", BlobName = "dummy.jpg" },
                AnonymizedBlobStorageLocation = new BlobStorageLocation { StorageAccount = "dummy", BlobContainer = "dummy", BlobName = "dummy.jpg" },
                VisualizedBlobStorageLocation = new BlobStorageLocation { StorageAccount = "dummy", BlobContainer = "dummy", BlobName = "dummy.jpg" },
                TagId = "dummy",
                InspectionDescription = "dummy",
                AnalysisToBeRun = new List<AnalysisType>()
            };
            var plantData = new PlantData { Id = "plantId" };
            _serviceMock.Setup(s => s.CreatePlantDataEntry(request)).ReturnsAsync(plantData);

            var result = await _controller.CreatePlantDataEntry(request);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(nameof(_controller.GetPlantDataById), createdResult.ActionName);
            Assert.Equal(plantData, createdResult.Value);
        }

        [Fact]
        public async Task CreatePlantDataEntry_Returns500_WhenServiceReturnsNull()
        {
            var request = new PlantDataRequest
            {
                InspectionId = "dummy",
                InstallationCode = "dummy",
                RawDataBlobStorageLocation = new BlobStorageLocation { StorageAccount = "dummy", BlobContainer = "dummy", BlobName = "dummy.jpg" },
                AnonymizedBlobStorageLocation = new BlobStorageLocation { StorageAccount = "dummy", BlobContainer = "dummy", BlobName = "dummy.jpg" },
                VisualizedBlobStorageLocation = new BlobStorageLocation { StorageAccount = "dummy", BlobContainer = "dummy", BlobName = "dummy.jpg" },
                TagId = "dummy",
                InspectionDescription = "dummy",
                AnalysisToBeRun = new List<AnalysisType>()
            };
            _serviceMock.Setup(s => s.CreatePlantDataEntry(request)).ReturnsAsync((PlantData)null!);

            IActionResult result = await _controller.CreatePlantDataEntry(request);

            var statusResult = Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(500, statusResult.StatusCode);
        }

        [Fact]
        public async Task CreatePlantDataEntry_ThrowsException_WhenServiceSavesToDatabase()
        {

            var request = new PlantDataRequest
            {
                InspectionId = "dummy",
                InstallationCode = "dummy",
                RawDataBlobStorageLocation = new BlobStorageLocation { StorageAccount = "dummy", BlobContainer = "dummy", BlobName = "dummy.jpg" },
                AnonymizedBlobStorageLocation = new BlobStorageLocation { StorageAccount = "dummy", BlobContainer = "dummy", BlobName = "dummy.jpg" },
                VisualizedBlobStorageLocation = new BlobStorageLocation { StorageAccount = "dummy", BlobContainer = "dummy", BlobName = "dummy.jpg" },
                TagId = "dummy",
                InspectionDescription = "dummy",
                AnalysisToBeRun = new List<AnalysisType>()
            };
            _serviceMock.Setup(s => s.CreatePlantDataEntry(request)).ThrowsAsync(new Exception("fail"));
            var result = await _controller.CreatePlantDataEntry(request);

            var statusResult = Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(500, statusResult.StatusCode);


        }
    }
}
