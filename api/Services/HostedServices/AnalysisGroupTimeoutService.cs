using api.Configurations;
using Microsoft.Extensions.Options;

namespace api.Services.HostedServices;

public class AnalysisGroupTimeoutService(
    IServiceProvider serviceProvider,
    IOptions<AnalysisOptions> analysisOptions,
    ILogger<AnalysisGroupTimeoutService> logger
) : BackgroundService
{
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(
        Math.Max(1, analysisOptions.Value.AnalysisGroupTimeoutCheckIntervalSeconds)
    );

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "AnalysisGroupTimeoutService started (check interval: {IntervalSeconds}s)",
            _checkInterval.TotalSeconds
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var processor =
                    scope.ServiceProvider.GetRequiredService<IAnalysisGroupTimeoutProcessor>();
                await processor.ProcessTimedOutGroups(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while processing timed-out analysis groups");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("AnalysisGroupTimeoutService stopped");
    }
}
