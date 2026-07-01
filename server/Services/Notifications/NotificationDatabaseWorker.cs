using Microsoft.Extensions.Options;

namespace Poseidon.Server.Services.Notifications;

public sealed class NotificationDatabaseWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<NotificationDatabaseWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        RabbitMqOptions rabbitOptions = options.Value;
        TimeSpan delay = TimeSpan.FromSeconds(Math.Max(1, rabbitOptions.PublisherPollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingBatchAsync(rabbitOptions, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Database notification worker failed while processing pending jobs.");
            }

            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task ProcessPendingBatchAsync(
        RabbitMqOptions rabbitOptions,
        CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        INotificationJobReader reader = scope.ServiceProvider.GetRequiredService<INotificationJobReader>();
        INotificationJobProcessor processor = scope.ServiceProvider.GetRequiredService<INotificationJobProcessor>();

        IReadOnlyList<PendingNotificationJob> pendingJobs = await reader.ReadPendingAsync(
            rabbitOptions.PublisherBatchSize,
            cancellationToken: cancellationToken);

        foreach (PendingNotificationJob job in pendingJobs)
        {
            NotificationJobProcessResult result = await processor.ProcessAsync(
                job.NotificationJobId,
                rabbitOptions.MaxDeliveryAttempts,
                cancellationToken);

            if (result.Status is NotificationJobProcessStatus.Failed or NotificationJobProcessStatus.UnsupportedType)
            {
                logger.LogWarning(
                    "Database notification worker processed job {NotificationJobId} with status {Status}: {Error}",
                    job.NotificationJobId,
                    result.Status,
                    result.Error);
            }
        }
    }
}
