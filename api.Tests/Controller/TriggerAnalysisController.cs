using System;
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
        public async Task TriggerAnonymizer_ReturnsNotFound_WhenPlantDataDoesNotExist()
        {
            //Arrange
            var plantDataId = Guid.NewGuid();
            _plantDataServiceMock
                .Setup(service => service.ReadById(plantDataId))
                .ReturnsAsync((PlantData?)null);

            //Act
            var result = await _triggerAnalysisController.TriggerAnonymizer(plantDataId);

            //Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task TriggerAnonymizer_ReturnsConflict_WhenWorkflowsAlreadyTriggered()
        {
            //Arrange
            var plantDataId = Guid.NewGuid();
            _plantDataServiceMock
                .Setup(service => service.ReadById(plantDataId))
                .ReturnsAsync(
                    new PlantData
                    {
                        InspectionId = "dummyInspectionId",
                        InstallationCode = "dummyInstallationCode",
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
                            Status = WorkflowStatus.Started,
                        },
                    }
                );

            //Act
            var result = await _triggerAnalysisController.TriggerAnonymizer(plantDataId);

            //Assert
            Assert.IsType<ConflictObjectResult>(result);
            var conflictResult = result as ConflictObjectResult;
            Assert.Equal(
                "A workflow in the analysis chain is still running. Wait until the full chain has completed before triggering it again.",
                conflictResult!.Value
            );
        }

        [Fact]
        public async Task TriggerAnonymizer_ReturnsConflict_WhenDownstreamWorkflowIsStillRunning()
        {
            // Arrange
            var plantDataId = Guid.NewGuid();

            _plantDataServiceMock
                .Setup(service => service.ReadById(plantDataId))
                .ReturnsAsync(
                    new PlantData
                    {
                        InspectionId = "dummyInspectionId",
                        InstallationCode = "dummyInstallationCode",
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
                            Status = WorkflowStatus.ExitSuccess,
                        },
                        FencillaAnalysis = new FencillaAnalysis
                        {
                            DestinationBlobStorageLocation = new BlobStorageLocation
                            {
                                StorageAccount = "dummyOutputStorageAccount",
                                BlobContainer = "dummyOutputBlobContainer",
                                BlobName = "dummyOutputBlobName",
                            },
                            SourceBlobStorageLocation = new BlobStorageLocation
                            {
                                StorageAccount = "dummySourceStorageAccount",
                                BlobContainer = "dummySourceBlobContainer",
                                BlobName = "dummySourceBlobName",
                            },
                            Status = WorkflowStatus.Started,
                        },
                    }
                );

            // Act
            var result = await _triggerAnalysisController.TriggerAnonymizer(plantDataId);

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(result);
            Assert.Equal(
                "A workflow in the analysis chain is still running. Wait until the full chain has completed before triggering it again.",
                conflictResult.Value
            );
        }

        [Fact]
        public async Task TriggerAnonymizer_TriggersWorkflow_WhenPlantDataExistsAndNotStarted()
        {
            //Arrange
            var plantDataId = Guid.NewGuid();

            var plantData = new PlantData
            {
                InspectionId = "dummyInspectionId",
                InstallationCode = "dummyInstallationCode",
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
                        BlobName = "dummyAnonBlobName",
                    },
                },
            };

            _plantDataServiceMock
                .Setup(service => service.ReadById(plantDataId))
                .ReturnsAsync(plantData);
            _argoWorkflowServiceMock.Setup(service =>
                service.TriggerAnonymizer(It.IsAny<string>(), It.IsAny<Anonymization>())
            );

            //Act
            var result = await _triggerAnalysisController.TriggerAnonymizer(plantDataId);

            //Assert
            Assert.IsType<OkObjectResult>(result);
            var okResult = result as OkObjectResult;
            Assert.Equal("Workflow chain triggered successfully.", okResult!.Value);
        }

        [Fact]
        public async Task TriggerAnonymizer_TriggersWorkflow_WhenWorkflowChainPreviouslySucceeded()
        {
            // Arrange
            var plantDataId = Guid.NewGuid();
            var plantData = new PlantData
            {
                InspectionId = "dummyInspectionId",
                InstallationCode = "dummyInstallationCode",
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
                        BlobName = "dummyAnonBlobName",
                    },
                    Status = WorkflowStatus.ExitSuccess,
                },
                CLOEAnalysis = new CLOEAnalysis
                {
                    SourceBlobStorageLocation = new BlobStorageLocation
                    {
                        StorageAccount = "dummySourceStorageAccount",
                        BlobContainer = "dummySourceBlobContainer",
                        BlobName = "dummySourceBlobName",
                    },
                    DestinationBlobStorageLocation = new BlobStorageLocation
                    {
                        StorageAccount = "dummyDestinationStorageAccount",
                        BlobContainer = "dummyDestinationBlobContainer",
                        BlobName = "dummyDestinationBlobName",
                    },
                    Status = WorkflowStatus.ExitSuccess,
                },
            };

            _plantDataServiceMock
                .Setup(service => service.ReadById(plantDataId))
                .ReturnsAsync(plantData);

            // Act
            var result = await _triggerAnalysisController.TriggerAnonymizer(plantDataId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal("Workflow chain triggered successfully.", okResult.Value);
            _argoWorkflowServiceMock.Verify(
                service =>
                    service.TriggerAnonymizer(plantData.InspectionId, plantData.Anonymization),
                Times.Once
            );
        }

        [Fact]
        public async Task TriggerAnonymizer_TriggersWorkflow_WhenWorkflowChainPreviouslyFailed()
        {
            // Arrange
            var plantDataId = Guid.NewGuid();
            var plantData = new PlantData
            {
                InspectionId = "dummyInspectionId",
                InstallationCode = "dummyInstallationCode",
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
                        BlobName = "dummyAnonBlobName",
                    },
                    Status = WorkflowStatus.ExitFailure,
                },
                ThermalReadingAnalysis = new ThermalReadingAnalysis
                {
                    SourceBlobStorageLocation = new BlobStorageLocation
                    {
                        StorageAccount = "dummySourceStorageAccount",
                        BlobContainer = "dummySourceBlobContainer",
                        BlobName = "dummySourceBlobName",
                    },
                    DestinationBlobStorageLocation = new BlobStorageLocation
                    {
                        StorageAccount = "dummyDestinationStorageAccount",
                        BlobContainer = "dummyDestinationBlobContainer",
                        BlobName = "dummyDestinationBlobName",
                    },
                    Status = WorkflowStatus.ExitFailure,
                },
            };

            _plantDataServiceMock
                .Setup(service => service.ReadById(plantDataId))
                .ReturnsAsync(plantData);

            // Act
            var result = await _triggerAnalysisController.TriggerAnonymizer(plantDataId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal("Workflow chain triggered successfully.", okResult.Value);
            _argoWorkflowServiceMock.Verify(
                service =>
                    service.TriggerAnonymizer(plantData.InspectionId, plantData.Anonymization),
                Times.Once
            );
        }

        [Fact]
        public async Task TriggerAnalysis_ReturnsNotFound_WhenPlantDataDoesNotExist()
        {
            // Arrange
            var plantDataId = Guid.NewGuid();

            _plantDataServiceMock
                .Setup(service => service.ReadById(plantDataId))
                .ReturnsAsync((PlantData?)null);

            // Act
            var result = await _triggerAnalysisController.TriggerAnalysis(plantDataId);

            // Assert
            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal($"Could not find plant data with id {plantDataId}", notFound.Value);
        }

        [Fact]
        public async Task TriggerAnalysis_ReturnsOk_WhenServiceSucceeds()
        {
            // Arrange
            var plantDataId = Guid.NewGuid();

            _plantDataServiceMock
                .Setup(service => service.ReadById(plantDataId))
                .ReturnsAsync(
                    new PlantData
                    {
                        InspectionId = "inspection-id",
                        InstallationCode = "dummy-installation-code",
                        Anonymization = new Anonymization
                        {
                            DestinationBlobStorageLocation = new BlobStorageLocation
                            {
                                StorageAccount = "dummyStorageAccount",
                                BlobContainer = "dummyBlobContainer",
                                BlobName = "dummyBlobName",
                            },
                            SourceBlobStorageLocation = new BlobStorageLocation
                            {
                                StorageAccount = "dummySourceStorageAccount",
                                BlobContainer = "dummySourceBlobContainer",
                                BlobName = "dummySourceBlobName",
                            },
                        },
                    }
                );

            // Act
            var result = await _triggerAnalysisController.TriggerAnalysis(plantDataId);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(
                "Triggering anonymization workflow which will trigger analysis workflows.",
                ok.Value
            );
        }

        [Fact]
        public async Task TriggerAnalysis_ReturnsConflict_WhenAnonymizationStarted()
        {
            // Arrange
            var plantDataId = Guid.NewGuid();

            _plantDataServiceMock
                .Setup(service => service.ReadById(plantDataId))
                .ReturnsAsync(
                    new PlantData
                    {
                        InspectionId = "inspection-id",
                        InstallationCode = "dummy-installation-code",
                        Anonymization = new Anonymization
                        {
                            DestinationBlobStorageLocation = new BlobStorageLocation
                            {
                                StorageAccount = "dummyStorageAccount",
                                BlobContainer = "dummyBlobContainer",
                                BlobName = "dummyBlobName",
                            },
                            SourceBlobStorageLocation = new BlobStorageLocation
                            {
                                StorageAccount = "dummySourceStorageAccount",
                                BlobContainer = "dummySourceBlobContainer",
                                BlobName = "dummySourceBlobName",
                            },
                            Status = WorkflowStatus.Started,
                        },
                    }
                );

            // Act
            var result = await _triggerAnalysisController.TriggerAnalysis(plantDataId);

            // Assert
            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Equal(
                "Anonymization is still in progress. Analysis workflows will be triggered once it completes.",
                conflict.Value
            );
        }

        [Fact]
        public async Task TriggerAnalysis_ReturnsOk_WhenAnonymizationExitSuccessAndNoConfiguredAnalyses()
        {
            // Arrange
            var plantDataId = Guid.NewGuid();

            _plantDataServiceMock
                .Setup(service => service.ReadById(plantDataId))
                .ReturnsAsync(
                    new PlantData
                    {
                        InspectionId = "inspection-id",
                        InstallationCode = "dummy-installation-code",
                        Anonymization = new Anonymization
                        {
                            DestinationBlobStorageLocation = new BlobStorageLocation
                            {
                                StorageAccount = "dummyStorageAccount",
                                BlobContainer = "dummyBlobContainer",
                                BlobName = "dummyBlobName",
                            },
                            SourceBlobStorageLocation = new BlobStorageLocation
                            {
                                StorageAccount = "dummySourceStorageAccount",
                                BlobContainer = "dummySourceBlobContainer",
                                BlobName = "dummySourceBlobName",
                            },
                            Status = WorkflowStatus.ExitSuccess,
                        },
                    }
                );

            // Act
            var result = await _triggerAnalysisController.TriggerAnalysis(plantDataId);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(
                $"No analysis workflows configured for plant data with Id {plantDataId}.",
                ok.Value
            );
        }

        [Fact]
        public async Task TriggerAnalysis_ReturnsOk_WhenAnonymizationExitSuccessAndConfiguredCLOEAnalysis()
        {
            // Arrange
            var plantDataId = Guid.NewGuid();

            _plantDataServiceMock
                .Setup(service => service.ReadById(plantDataId))
                .ReturnsAsync(
                    new PlantData
                    {
                        InspectionId = "inspection-id",
                        InstallationCode = "dummy-installation-code",
                        Anonymization = new Anonymization
                        {
                            DestinationBlobStorageLocation = new BlobStorageLocation
                            {
                                StorageAccount = "dummyStorageAccount",
                                BlobContainer = "dummyBlobContainer",
                                BlobName = "dummyBlobName",
                            },
                            SourceBlobStorageLocation = new BlobStorageLocation
                            {
                                StorageAccount = "dummySourceStorageAccount",
                                BlobContainer = "dummySourceBlobContainer",
                                BlobName = "dummySourceBlobName",
                            },
                            Status = WorkflowStatus.ExitSuccess,
                        },
                        CLOEAnalysis = new CLOEAnalysis
                        {
                            SourceBlobStorageLocation = new BlobStorageLocation
                            {
                                StorageAccount = "dummySourceStorageAccount",
                                BlobContainer = "dummySourceBlobContainer",
                                BlobName = "dummySourceBlobName",
                            },
                            DestinationBlobStorageLocation = new BlobStorageLocation
                            {
                                StorageAccount = "dummyDestinationStorageAccount",
                                BlobContainer = "dummyDestinationBlobContainer",
                                BlobName = "dummyDestinationBlobName",
                            },
                            Status = WorkflowStatus.NotStarted,
                        },
                    }
                );

            // Act
            var result = await _triggerAnalysisController.TriggerAnalysis(plantDataId);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal($"Triggered analysis workflows: CLOE analysis", ok.Value);
        }
    }
}
