namespace Poseidon.Server.Services.Notifications;

public interface INotificationJobProcessor
{
    Task<NotificationJobProcessResult> ProcessAsync(
        Guid notificationJobId,
        int maxAttempts = 5,
        CancellationToken cancellationToken = default);
}

public enum NotificationJobProcessStatus
{
    Succeeded,
    NotFound,
    NotPending,
    UnsupportedType,
    Failed
}

public sealed record NotificationJobProcessResult(
    NotificationJobProcessStatus Status,
    string? Error = null);
