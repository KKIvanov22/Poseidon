using Microsoft.EntityFrameworkCore;
using Poseidon.Server.Data;
using Poseidon.Server.Data.Entities;

namespace Poseidon.Server.Services.Notifications;

public sealed class NotificationJobCompletionService(AppDbContext dbContext) : INotificationJobCompletionService
{
    private const int PendingStatusId = 1;
    private const int ProcessingStatusId = 2;
    private const int SucceededStatusId = 3;

    public async Task<NotificationJobCompletionResult> MarkCompletedAsync(
        Guid notificationJobId,
        CancellationToken cancellationToken = default)
    {
        NotificationJob? job = await dbContext.NotificationJobs
            .SingleOrDefaultAsync(
                notificationJob => notificationJob.NotificationJobId == notificationJobId,
                cancellationToken);

        if (job is null)
        {
            return new NotificationJobCompletionResult(NotificationJobCompletionStatus.NotFound);
        }

        if (job.JobStatusId == SucceededStatusId)
        {
            await EnsureSucceededDeliveryAsync(job, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new NotificationJobCompletionResult(NotificationJobCompletionStatus.Completed, job);
        }

        if (job.JobStatusId is not (PendingStatusId or ProcessingStatusId))
        {
            return new NotificationJobCompletionResult(
                NotificationJobCompletionStatus.CannotComplete,
                job,
                "Only pending or processing notification jobs can be completed.");
        }

        job.JobStatusId = SucceededStatusId;
        job.Attempts++;
        job.ProcessedAt = DateTimeOffset.UtcNow;
        job.PublisherLockedUntil = null;
        job.LastError = null;

        await EnsureSucceededDeliveryAsync(job, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new NotificationJobCompletionResult(NotificationJobCompletionStatus.Completed, job);
    }

    private async Task EnsureSucceededDeliveryAsync(
        NotificationJob job,
        CancellationToken cancellationToken)
    {
        bool deliveryExists = await dbContext.NotificationDeliveries.AnyAsync(
            delivery => delivery.NotificationJobId == job.NotificationJobId &&
                delivery.Result == "Succeeded",
            cancellationToken);

        if (deliveryExists)
        {
            return;
        }

        dbContext.NotificationDeliveries.Add(new NotificationDelivery
        {
            NotificationJobId = job.NotificationJobId,
            RecipientUserId = job.RecipientUserId,
            Channel = job.Channel,
            Result = "Succeeded"
        });
    }
}
