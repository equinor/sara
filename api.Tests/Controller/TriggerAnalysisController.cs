using System.Collections.Generic;
using System.Threading.Tasks;
using api.Database.Models;
using api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace api.Controllers.Tests
{
    public class TriggerAnalysisControllerTest
    {
        private readonly Mock<ILogger<TriggerAnalysisController>> _loggerMock;
        private readonly Mock<IArgoWorkflowService> _argoWorkflowServiceMock;
        private readonly Mock<IPlantDataService> _plantDataServiceMock;
        private readonly TriggerAnalysisController _triggerAnalysisController;

        public TriggerAnalysisControllerTest()
        {
            _loggerMock = new Mock<ILogger<TriggerAnalysisController>>();
            _argoWorkflowServiceMock = new Mock<IArgoWorkflowService>();
            _plantDataServiceMock = new Mock<IPlantDataService>();
            _triggerAnalysisController = new TriggerAnalysisController(
                _argoWorkflowServiceMock.Object,
                _plantDataServiceMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task TriggerAnalysis_ReturnsNotFound_WhenPlantDataDoesNotExist()
        {
            //Arrange
            var plantDataId = "nonexistent-id";
            _plantDataServiceMock.Setup(service => service.ReadById(plantDataId)).ReturnsAsync((PlantData?)null);

            //Act
            var result = await _triggerAnalysisController.TriggerAnalysis(plantDataId);

            //Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task TriggerAnalysis_ReturnsConflict_WhenWorkflowsAlreadyTriggered()
        {
            //Arrange
            var plantDataId = "dummy-id";
            _plantDataServiceMock.Setup(service => service.ReadById(plantDataId)).ReturnsAsync(new PlantData
            {
                AnonymizerWorkflowStatus = WorkflowStatus.Started
            });

            //Act
            var result = await _triggerAnalysisController.TriggerAnalysis(plantDataId);

            //Assert
            Assert.IsType<ConflictObjectResult>(result);
            var conflictResult = result as ConflictObjectResult;
            Assert.Equal("Analysis has already been triggered and will not run again.", conflictResult!.Value);
        }

        [Fact]
        public async Task TriggerAnalysis_TriggersWorkflow_WhenPlantDataExistsAndNotStarted()
        {
            //Arrange
            var plantDataId = "dummy-id";
            var plantData = new PlantData
            {
                AnonymizerWorkflowStatus = WorkflowStatus.NotStarted,
                AnalysisToBeRun = new List<AnalysisType>
                {
                    AnalysisType.ConstantLevelOiler,
                    AnalysisType.Fencilla
                }
            };
            _plantDataServiceMock.Setup(service => service.ReadById(plantDataId)).ReturnsAsync(plantData);
            _argoWorkflowServiceMock.Setup(service => service.TriggerAnalysis(
                It.IsAny<PlantData>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()
            ));

            //Act
            var result = await _triggerAnalysisController.TriggerAnalysis(plantDataId);

            //Assert
            Assert.IsType<OkObjectResult>(result);
            var okResult = result as OkObjectResult;
            Assert.Equal("Analysis workflow triggered successfully.", okResult!.Value);
        }

    }
}
