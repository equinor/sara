using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api.Services;

namespace Api.Test.Mocks;

/// <summary>
/// Test fake for <see cref="ITimeseriesService"/> that records every upload
/// request for later inspection by tests. CO2 fetches return null.
/// </summary>
public class RecordingTimeseriesService : ITimeseriesService
{
    private readonly ConcurrentQueue<TriggerTimeseriesUploadRequest> _uploads = new();

    public IReadOnlyCollection<TriggerTimeseriesUploadRequest> Uploads => _uploads.ToArray();
    public Func<TriggerTimeseriesUploadRequest, Task>? BeforeUpload { get; set; }

    public async Task TriggerTimeseriesUpload(TriggerTimeseriesUploadRequest uploadRequest)
    {
        if (BeforeUpload is not null)
            await BeforeUpload(uploadRequest);
        _uploads.Enqueue(uploadRequest);
    }

    public Task<double?> FetchCO2ConcentrationFromTimeseries(
        FetchCO2MeasurementRequest fetchRequest
    )
    {
        return Task.FromResult<double?>(null);
    }

    public void Reset()
    {
        _uploads.Clear();
    }
}
