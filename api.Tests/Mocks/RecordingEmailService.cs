using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api.Services;

namespace Api.Test.Mocks;

/// <summary>
/// Test fake for <see cref="IEmailService"/> that records every send for later
/// inspection. Set <see cref="ThrowOnSend"/> to simulate transient failures.
/// </summary>
public class RecordingEmailService : IEmailService
{
    public record FencillaEmail(string InspectionId, float? Confidence, string Installation);

    private readonly ConcurrentQueue<FencillaEmail> _fencillaEmails = new();

    public IReadOnlyCollection<FencillaEmail> FencillaEmails => _fencillaEmails.ToArray();

    public bool ThrowOnSend { get; set; }

    public Task SendMailAsync(
        string subject,
        string body,
        EmailTarget recipient,
        string installation
    )
    {
        if (ThrowOnSend)
            throw new InvalidOperationException("RecordingEmailService configured to throw");
        return Task.CompletedTask;
    }

    public Task SendFencillaResultEmail(string inspectionId, float? confidence, string installation)
    {
        if (ThrowOnSend)
            throw new InvalidOperationException("RecordingEmailService configured to throw");
        _fencillaEmails.Enqueue(new FencillaEmail(inspectionId, confidence, installation));
        return Task.CompletedTask;
    }
}
