using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using api.Services;

namespace Api.Test.Mocks;

/// <summary>
/// In-memory <see cref="IArgoWorkflowSubmitter"/> fake for tests. Records every
/// submitted manifest and every <c>DeleteByLabelAsync</c> call, and serves
/// <c>ListByLabelAsync</c> from a programmable in-memory store keyed by label
/// selector. Tests stage CR JSON via <see cref="SetListResponse"/> to drive the
/// reconciler without a real cluster.
/// </summary>
public class RecordingArgoWorkflowSubmitter : IArgoWorkflowSubmitter
{
    private readonly ConcurrentQueue<JsonObject> _submitted = new();
    private readonly ConcurrentQueue<string> _deletedSelectors = new();
    private readonly ConcurrentDictionary<string, JsonArray> _listResponses = new();
    private int _submitCounter;

    public IReadOnlyCollection<JsonObject> SubmittedManifests => _submitted.ToArray();
    public IReadOnlyCollection<string> DeletedLabelSelectors => _deletedSelectors.ToArray();

    /// <summary>
    /// Stages the response returned by <see cref="ListByLabelAsync"/> for the
    /// given label selector. Pass an empty array to simulate "no CRs found".
    /// </summary>
    public void SetListResponse(string labelSelector, JsonArray items)
    {
        _listResponses[labelSelector] = items;
    }

    public Task<string> SubmitAsync(JsonObject manifest, CancellationToken ct = default)
    {
        _submitted.Enqueue(manifest);
        var generated = manifest["metadata"]?["generateName"]?.GetValue<string>() ?? "wf-";
        var name = $"{generated}{Interlocked.Increment(ref _submitCounter):D4}";
        return Task.FromResult(name);
    }

    public Task<JsonArray> ListByLabelAsync(string labelSelector, CancellationToken ct = default)
    {
        return Task.FromResult(
            _listResponses.TryGetValue(labelSelector, out var items) ? items : []
        );
    }

    public Task<int> DeleteByLabelAsync(string labelSelector, CancellationToken ct = default)
    {
        _deletedSelectors.Enqueue(labelSelector);
        var count = _listResponses.TryGetValue(labelSelector, out var items) ? items.Count : 0;
        _listResponses.TryRemove(labelSelector, out _);
        return Task.FromResult(count);
    }
}
