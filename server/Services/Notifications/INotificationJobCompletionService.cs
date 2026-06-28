using Poseidon.Server.Data.Entities;

namespace Poseidon.Server.Services.Notifications;

public interface INotificationJobCompletionService
{
    Task<NotificationJobCompletionResult> MarkCompletedAsync(
        Guid notificationJobId,
        CancellationToken cancellationToken = default);
}

public enum NotificationJobCompletionStatus
{
    Completed,
    NotFound,
    CannotComplete
}

public sealed record NotificationJobCompletionResult(
    NotificationJobCompletionStatus Status,
    NotificationJob? Job = null,
    string? Problem = null);
