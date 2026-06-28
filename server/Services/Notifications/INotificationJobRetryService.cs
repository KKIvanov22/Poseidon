using Poseidon.Server.Data.Entities;

namespace Poseidon.Server.Services.Notifications;

public interface INotificationJobRetryService
{
    Task<NotificationJobRetryResult> RetryAsync(
        Guid notificationJobId,
        CancellationToken cancellationToken = default);
}

public enum NotificationJobRetryStatus
{
    Retried,
    NotFound,
    CannotRetry
}

public sealed record NotificationJobRetryResult(
    NotificationJobRetryStatus Status,
    NotificationJob? Job = null,
    string? Problem = null);
