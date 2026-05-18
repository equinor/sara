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
    private readonly ConcurrentQueue<SaraVisualizationAvailableMessage> _visualizationMessages =
        new();
    private readonly ConcurrentQueue<SaraAnalysisResultMessage> _analysisResultMessages = new();

    public IReadOnlyCollection<SaraVisualizationAvailableMessage> VisualizationMessages =>
        _visualizationMessages.ToArray();

    public IReadOnlyCollection<SaraAnalysisResultMessage> AnalysisResultMessages =>
        _analysisResultMessages.ToArray();

    public Task PublishSaraVisualizationAvailable(
        SaraVisualizationAvailableMessage visualizationAvailableMessage
    )
    {
        _visualizationMessages.Enqueue(visualizationAvailableMessage);
        return Task.CompletedTask;
    }

    public Task PublishSaraAnalysisResultAvailable(
        SaraAnalysisResultMessage saraAnalysisResultMessage
    )
    {
        _analysisResultMessages.Enqueue(saraAnalysisResultMessage);
        return Task.CompletedTask;
    }

    public void Reset()
    {
        _visualizationMessages.Clear();
        _analysisResultMessages.Clear();
    }
}
