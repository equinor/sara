using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Api.Test.Mocks;

/// <summary>
/// Test fake <see cref="HttpMessageHandler"/> that records every outbound
/// request and returns a configurable response. Intended to back a named
/// HttpClient (e.g. the "Argo" client used by WorkflowService).
/// </summary>
public class RecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly ConcurrentQueue<RecordedHttpRequest> _requests = new();

    public HttpStatusCode ResponseStatusCode { get; set; } = HttpStatusCode.OK;

    public string ResponseBody { get; set; } = string.Empty;

    public IReadOnlyCollection<RecordedHttpRequest> Requests => _requests.ToArray();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        string body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);

        _requests.Enqueue(new RecordedHttpRequest(request.Method, request.RequestUri, body));

        return new HttpResponseMessage(ResponseStatusCode)
        {
            Content = new StringContent(ResponseBody),
        };
    }

    public void Reset()
    {
        _requests.Clear();
        ResponseStatusCode = HttpStatusCode.OK;
        ResponseBody = string.Empty;
    }
}

public record RecordedHttpRequest(HttpMethod Method, Uri? RequestUri, string Body);
