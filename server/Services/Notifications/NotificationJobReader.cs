using Microsoft.EntityFrameworkCore;
using Poseidon.Server.Data;

namespace Poseidon.Server.Services.Notifications;

public sealed class NotificationJobReader(AppDbContext dbContext) : INotificationJobReader
{
    private const int PendingStatusId = 1;
    private const int DefaultLimit = 25;
    private const int MaxLimit = 100;

    public async Task<IReadOnlyList<PendingNotificationJob>> ReadPendingAsync(
        int limit,
        DateTimeOffset? availableAtOrBefore = null,
        CancellationToken cancellationToken = default)
    {
        int boundedLimit = limit <= 0 ? DefaultLimit : Math.Min(limit, MaxLimit);
        DateTimeOffset dueAt = availableAtOrBefore ?? DateTimeOffset.UtcNow;

        return await dbContext.NotificationJobs
            .AsNoTracking()
            .Where(job => job.JobStatusId == PendingStatusId && job.AvailableAt <= dueAt)
            .OrderBy(job => job.CreatedAt)
            .ThenBy(job => job.NotificationJobId)
            .Take(boundedLimit)
            .Select(job => new PendingNotificationJob(
                job.NotificationJobId,
                job.EventId,
                job.RecipientUserId,
                job.Channel,
                job.Title,
                job.Message,
                job.Payload,
                job.Attempts,
                job.AvailableAt,
                job.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
