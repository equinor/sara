using k8s.Autorest;

namespace api.Services.HostedServices;

public class ArgoWorkflowWatcherService(
    IArgoWorkflowClient client,
    IServiceScopeFactory scopeFactory,
    ILogger<ArgoWorkflowWatcherService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var snapshot = await client.ListWorkflows(stoppingToken);
                foreach (var workflow in snapshot.Items)
                {
                    await Process(workflow, stoppingToken);
                }

                await foreach (
                    var workflow in client.WatchWorkflows(snapshot.ResourceVersion, stoppingToken)
                )
                {
                    await Process(workflow, stoppingToken);
                }
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

    private async Task Process(ArgoWorkflowResource workflow, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        await scope
            .ServiceProvider.GetRequiredService<IArgoWorkflowEventProcessor>()
            .Process(workflow, cancellationToken);
    }
}
