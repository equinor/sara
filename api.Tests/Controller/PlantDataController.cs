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
        private readonly Mock<IPlantDataService> _plantDataServiceMock;
        private readonly Mock<IAnalysisMappingService> _analysisMappingServiceMock;
        private readonly PlantDataController _plantDataController;

        public PlantDataControllerTest()
        {
            _loggerMock = new Mock<ILogger<PlantDataController>>();
            _plantDataServiceMock = new Mock<IPlantDataService>();
            _analysisMappingServiceMock = new Mock<IAnalysisMappingService>();
            _plantDataController = new PlantDataController(_loggerMock.Object, _plantDataServiceMock.Object, _analysisMappingServiceMock.Object);
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

            var result = await _plantDataController.CreatePlantDataEntry(request);

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
            var result = await _plantDataController.CreatePlantDataEntry(request);

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
            _plantDataServiceMock.Setup(s => s.CreatePlantDataEntry(request)).ReturnsAsync(plantData);

            var result = await _plantDataController.CreatePlantDataEntry(request);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(nameof(_plantDataController.GetPlantDataById), createdResult.ActionName);
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
            _plantDataServiceMock.Setup(s => s.CreatePlantDataEntry(request)).ReturnsAsync((PlantData)null!);

            IActionResult result = await _plantDataController.CreatePlantDataEntry(request);

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
            _plantDataServiceMock.Setup(s => s.CreatePlantDataEntry(request)).ThrowsAsync(new Exception("fail"));
            var result = await _plantDataController.CreatePlantDataEntry(request);

            var statusResult = Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(500, statusResult.StatusCode);


        }

        [Fact]
        public async Task CreatePlantDataEntry_AddsAnalysesFromMapping_WhenMappingExists()
        {
            var request = new PlantDataRequest
            {
                InspectionId = "dummy",
                InstallationCode = "dummy",
                RawDataBlobStorageLocation = new BlobStorageLocation { StorageAccount = "dummy", BlobContainer = "dummy", BlobName = "dummy.jpg" },
                AnonymizedBlobStorageLocation = new BlobStorageLocation { StorageAccount = "dummy", BlobContainer = "dummy", BlobName = "dummy.jpg" },
                VisualizedBlobStorageLocation = new BlobStorageLocation { StorageAccount = "dummy", BlobContainer = "dummy", BlobName = "dummy.jpg" },
                TagId = "tag1",
                InspectionDescription = "desc1",
                AnalysisToBeRun = new List<AnalysisType> { AnalysisType.ConstantLevelOiler }
            };

            var analysisMapping = new AnalysisMapping("desc1", "tag1")
            {
                Id = "mapping1",
                AnalysesToBeRun = new List<AnalysisType> { AnalysisType.Anonymizer, AnalysisType.Fencilla }
            };

            _analysisMappingServiceMock
                .Setup(s => s.ReadByInspectionDescriptionAndTag("desc1", "tag1"))
                .ReturnsAsync(analysisMapping);

            _plantDataServiceMock
                .Setup(s => s.CreatePlantDataEntry(It.IsAny<PlantDataRequest>()))
                .ReturnsAsync((PlantDataRequest req) => new PlantData
                {
                    Id = "plantId",
                    AnalysisToBeRun = req.AnalysisToBeRun
                });

            var result = await _plantDataController.CreatePlantDataEntry(request);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            var returnedPlantData = Assert.IsType<PlantData>(createdResult.Value);

            Assert.Contains(AnalysisType.ConstantLevelOiler, returnedPlantData.AnalysisToBeRun);
            Assert.Contains(AnalysisType.Anonymizer, returnedPlantData.AnalysisToBeRun);
            Assert.Contains(AnalysisType.Fencilla, returnedPlantData.AnalysisToBeRun);
        }
    }
}
