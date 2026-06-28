using Microsoft.EntityFrameworkCore;
using Poseidon.Server.Data;
using Poseidon.Server.Data.Entities;

namespace Poseidon.Server.Services.Notifications;

public sealed class NotificationJobRetryService(AppDbContext dbContext) : INotificationJobRetryService
{
    private const int PendingStatusId = 1;
    private const int FailedStatusId = 4;

    public async Task<NotificationJobRetryResult> RetryAsync(
        Guid notificationJobId,
        CancellationToken cancellationToken = default)
    {
        NotificationJob? job = await dbContext.NotificationJobs
            .SingleOrDefaultAsync(
                notificationJob => notificationJob.NotificationJobId == notificationJobId,
                cancellationToken);

        if (job is null)
        {
            return new NotificationJobRetryResult(NotificationJobRetryStatus.NotFound);
        }

        if (job.JobStatusId != FailedStatusId)
        {
            return new NotificationJobRetryResult(
                NotificationJobRetryStatus.CannotRetry,
                job,
                "Only failed notification jobs can be retried.");
        }

        job.JobStatusId = PendingStatusId;
        job.Attempts = 0;
        job.AvailableAt = DateTimeOffset.UtcNow;
        job.PublisherLockedUntil = null;
        job.PublishedAt = null;
        job.ProcessedAt = null;
        job.LastError = null;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new NotificationJobRetryResult(NotificationJobRetryStatus.Retried, job);
    }
}
