using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api.MQTT;

namespace Api.Test.Mocks;

/// <summary>
/// Test fake for <see cref="IMqttPublisherService"/> that records every
/// publication for later inspection by tests.
/// </summary>
public class RecordingMqttPublisher : IMqttPublisherService
{
    public Func<Guid, Task>? BeforePublish { get; set; }

    private readonly ConcurrentQueue<SaraVisualizationAvailableMessage> _visualizationMessages =
        new();
    private readonly ConcurrentQueue<SaraAnalysisResultMessage> _analysisResultMessages = new();

    public IReadOnlyCollection<SaraVisualizationAvailableMessage> VisualizationMessages =>
        _visualizationMessages.ToArray();

    public IReadOnlyCollection<SaraAnalysisResultMessage> AnalysisResultMessages =>
        _analysisResultMessages.ToArray();

    public async Task PublishSaraVisualizationAvailable(
        SaraVisualizationAvailableMessage visualizationAvailableMessage
    )
    {
        if (BeforePublish is not null)
            await BeforePublish(visualizationAvailableMessage.WorkflowId);
        _visualizationMessages.Enqueue(visualizationAvailableMessage);
    }

    public async Task PublishSaraAnalysisResultAvailable(
        SaraAnalysisResultMessage saraAnalysisResultMessage
    )
    {
        if (BeforePublish is not null)
            await BeforePublish(saraAnalysisResultMessage.WorkflowId);
        _analysisResultMessages.Enqueue(saraAnalysisResultMessage);
    }

    public void Reset()
    {
        _visualizationMessages.Clear();
        _analysisResultMessages.Clear();
    }
}
