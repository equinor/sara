using System.Text.Json;
// using api.Controllers.Models;
// using api.Models;
// using api.MQTT;
// using api.MQTT.Events;
// using api.MQTT.MessageModels;
using api.Services;
using api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace api.MQTT
{
    /// <summary>
    ///     A background service which listens to events and performs callback functions.
    /// </summary>
    public class MqttEventHandler : EventHandlerBase
    {
        private readonly ILogger<MqttEventHandler> _logger;


        private readonly IServiceScopeFactory _scopeFactory;

        public MqttEventHandler(ILogger<MqttEventHandler> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            // Reason for using factory: https://www.thecodebuzz.com/using-dbcontext-instance-in-ihostedservice/
            _scopeFactory = scopeFactory;

            Subscribe();
        }

        private IInspectionDataService InspectionDataService => _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<IInspectionDataService>();
        private IAnonymizerService AnonymizerService => _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<IAnonymizerService>();

        public override void Subscribe()
        {
            MqttService.MqttIsarInspectionResultReceived += OnIsarInspectionResult;
        }

        public override void Unsubscribe()
        {
            MqttService.MqttIsarInspectionResultReceived -= OnIsarInspectionResult;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) { await stoppingToken; }


        private async void OnIsarInspectionResult(object? sender, MqttReceivedArgs mqttArgs)
        {
            var isarInspectionResultMessage = (IsarInspectionResultMessage)mqttArgs.Message;

            var inspectionResult = await InspectionDataService.ReadByInspectionId(isarInspectionResultMessage.InspectionId);

            if (inspectionResult != null)
            {
                _logger.LogWarning("Inspection Data with inspection id {InspectionId} already exists", isarInspectionResultMessage.InspectionId);
                return;
            }

            var inspectionData = await InspectionDataService.CreateFromMqttMessage(isarInspectionResultMessage);
            await AnonymizerService.TriggerAnonymizerFunc(inspectionData);
        }
    }
}
