using k8s.Autorest;

namespace api.Services.HostedServices;

/// <summary>
/// Watches SARA-managed Argo Workflows and forwards their state changes for processing.
/// The current workflow list is processed before watching from its resource version so
/// events that occurred while the service was unavailable are not missed.
/// </summary>
public class ArgoWorkflowWatcherService(
    IArgoWorkflowClient client,
    IServiceScopeFactory scopeFactory,
    ILogger<ArgoWorkflowWatcherService> logger
) : BackgroundService
{
    /// <summary>
    /// Maintains a continuous Kubernetes workflow watch until the application stops.
    /// Each iteration opens a streaming watch and normally remains there while events arrive;
    /// this is not a polling loop. Kubernetes watch connections are not guaranteed to remain
    /// open, so the loop creates a new workflow snapshot and watch when the connection closes.
    /// Expired resource versions are relisted immediately, while unexpected failures are
    /// retried after a delay.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await WatchWorkflowsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (HttpOperationException ex)
                when (ex.Response?.StatusCode == System.Net.HttpStatusCode.Gone)
            {
                logger.LogInformation("Argo Workflow watch resource version expired; relisting");
            }
            catch (ArgoWorkflowWatchException ex) when (ex.StatusCode == 410)
            {
                logger.LogInformation("Argo Workflow watch resource version expired; relisting");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Argo Workflow watch failed; relisting");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    /// <summary>
    /// Lists and processes all existing SARA-managed workflows before starting the watch.
    /// The list provides both the latest workflow states and a Kubernetes resource version.
    /// Watching from that version then delivers subsequent changes without leaving a gap
    /// between reading the current state and receiving new events.
    /// </summary>
    private async Task WatchWorkflowsAsync(CancellationToken cancellationToken)
    {
        var snapshot = await client.ListWorkflowsAsync(cancellationToken);
        logger.LogInformation(
            "Argo Workflow watch starting from resource version {ResourceVersion} with {WorkflowCount} workflows",
            snapshot.ResourceVersion,
            snapshot.Items.Count
        );
        foreach (var workflow in snapshot.Items)
        {
            await HandleWorkflowEventAsync(workflow, cancellationToken);
        }

        await foreach (
            var workflow in client.WatchWorkflowsAsync(snapshot.ResourceVersion, cancellationToken)
        )
        {
            await HandleWorkflowEventAsync(workflow, cancellationToken);
        }
    }

    /// <summary>
    /// Creates an isolated dependency-injection scope for each workflow event because the processor
    /// uses scoped services such as <see cref="api.Database.Context.SaraDbContext"/>. This gives each
    /// event a fresh database context and disposes it after processing, rather than retaining scoped
    /// state for the lifetime of the hosted watcher.
    /// </summary>
    private async Task HandleWorkflowEventAsync(
        ArgoWorkflowResource workflow,
        CancellationToken cancellationToken
    )
    {
        using var scope = scopeFactory.CreateScope();
        await scope
            .ServiceProvider.GetRequiredService<IArgoWorkflowEventProcessor>()
            .HandleWorkflowEventAsync(workflow, cancellationToken);
    }
}
