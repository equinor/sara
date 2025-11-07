using api.MQTT;
using api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using Xunit;

namespace api.Tests.MQTT
{
    public class MqttEventHandlerTests
    {
        private class MockedServices
        {
            public Mock<IServiceProvider> ServiceProviderMock { get; } = new();
            public Mock<IPlantDataService> PlantDataServiceMock { get; } = new();
            public Mock<IAnalysisMappingService> AnalysisMappingServiceMock { get; } = new();
            public Mock<IArgoWorkflowService> ArgoWorkflowServiceMock { get; } = new();
            public Mock<ILogger<MqttEventHandler>> LoggerMock { get; } = new();
            public Mock<ILogger<MqttMessageService>> MqttMessageServiceLoggerMock { get; } = new();
            public MqttEventHandler MqttEventHandler { get; }
            // public MqttMessageService MqttMessageService { get; }

            public MockedServices()
            {
                // Mock the PlantDataService to return null and do nothing
                ServiceProviderMock.Setup(sp => sp.GetService(typeof(IPlantDataService))).Returns(PlantDataServiceMock.Object);

                // Mock AnalysisMappingService
                ServiceProviderMock.Setup(sp => sp.GetService(typeof(IAnalysisMappingService))).Returns(AnalysisMappingServiceMock.Object);
                AnalysisMappingServiceMock.Setup(s => s.GetAnalysisTypeFromInspectionDescriptionAndTag(It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync([]);

                // Mock ArgoWorkflowService to return null and do nothing
                ServiceProviderMock.Setup(sp => sp.GetService(typeof(IArgoWorkflowService))).Returns(ArgoWorkflowServiceMock.Object);

                // I want to mock the IConfiguration properly and set configuration.GetSection("Storage")["RawStorageAccount"] = "dummy"
                var configurationMock = new Mock<IConfiguration>();
                var storageSectionMock = new Mock<IConfigurationSection>();
                storageSectionMock.Setup(s => s["RawStorageAccount"]).Returns("dummyRawStorageAccount");
                storageSectionMock.Setup(s => s["AnonStorageAccount"]).Returns("dummyAnonStorageAccount");
                storageSectionMock.Setup(s => s["VisStorageAccount"]).Returns("dummyVisStorageAccount");
                configurationMock.Setup(c => c.GetSection("Storage")).Returns(storageSectionMock.Object);

                // I want to add the real implementation of MqttMessageService, but with mocked dependencies
                IMqttMessageService mqttMessageService;
                mqttMessageService = new MqttMessageService(
                    MqttMessageServiceLoggerMock.Object,
                    configurationMock.Object,
                    AnalysisMappingServiceMock.Object,
                    PlantDataServiceMock.Object
                );

                ServiceProviderMock.Setup(sp => sp.GetService(typeof(IMqttMessageService))).Returns(mqttMessageService);

                // Setup scope factory to return service provider
                var scopeMock = new Mock<IServiceScope>();
                scopeMock.Setup(s => s.ServiceProvider).Returns(ServiceProviderMock.Object);
                var scopeFactoryMock = new Mock<IServiceScopeFactory>();
                scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

                MqttEventHandler = new MqttEventHandler(LoggerMock.Object, scopeFactoryMock.Object);
            }
        }

        [Fact]
        public void OnIsarInspectionResult_ValidMessage_TriggersAnalysis()
        {
            // Arrange
            var mockedServices = new MockedServices();

            var dummyMessage = new IsarInspectionResultMessage
            {
                ISARID = "dummy",
                RobotName = "dummy",
                InspectionId = "dummy",
                InspectionDataPath = new InspectionPathMessage { StorageAccount = "dummyRawStorageAccount", BlobContainer = "dummy", BlobName = "dummy" },
                InspectionMetadataPath = new InspectionPathMessage { StorageAccount = "dummyAnonStorageAccount", BlobContainer = "dummy", BlobName = "dummy" },
                InstallationCode = "dummy",
                TagID = "dummy",
                InspectionType = "dummy",
                InspectionDescription = "dummy",
                Timestamp = DateTime.UtcNow,
            };
            var mqttArgs = new MqttReceivedArgs(dummyMessage);

            // Act
            var methodInfo = mockedServices.MqttEventHandler.GetType()
                .GetMethod("OnIsarInspectionResult", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            methodInfo?.Invoke(mockedServices.MqttEventHandler, [null, mqttArgs]);

            // Assert services are called as expected
            mockedServices.PlantDataServiceMock.Verify(s => s.ReadByInspectionId("dummy"), Times.Once);
            mockedServices.AnalysisMappingServiceMock.Verify(s => s.GetAnalysisTypeFromInspectionDescriptionAndTag("dummy", "dummy"), Times.Once);
            mockedServices.PlantDataServiceMock.Verify(s => s.WritePlantData(It.IsAny<Database.Models.PlantData>()), Times.Once);
            mockedServices.ArgoWorkflowServiceMock.Verify(s => s.TriggerAnonymizer(It.IsAny<string>(), It.IsAny<Database.Models.Anonymization>()), Times.Once);
        }


        [Fact]
        public void OnIsarInspectionResult_InvalidMessage_LogsError()
        {
            // Arrange
            var mockedServices = new MockedServices();

            var dummyMessage = new IsarInspectionResultMessage
            {
                ISARID = null,
                RobotName = null,
                InspectionId = null,
                InspectionDataPath = new InspectionPathMessage { StorageAccount = null, BlobContainer = "dummy", BlobName = "dummy" },
                InspectionMetadataPath = new InspectionPathMessage { StorageAccount = "dummy", BlobContainer = null, BlobName = null },
                InstallationCode = null,
                TagID = null,
                InspectionType = null,
                InspectionDescription = null,
                Timestamp = DateTime.UtcNow,
            };
            var mqttArgs = new MqttReceivedArgs(dummyMessage);

            // Act
            var methodInfo = mockedServices.MqttEventHandler.GetType()
                .GetMethod("OnIsarInspectionResult", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            methodInfo?.Invoke(mockedServices.MqttEventHandler, [null, mqttArgs]);

            // Verify error log
#pragma warning disable CS8602 // Dereference of a possibly null reference, because v can be null
            mockedServices.LoggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v != null && v.ToString().Contains("Message validation error: ") && v.ToString().Contains("field is required.")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ), Times.Exactly(10) // Log one error per null value in the message
            );
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        }
    }
}
