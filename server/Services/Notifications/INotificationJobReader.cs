namespace Poseidon.Server.Services.Notifications;

public interface INotificationJobReader
{
    Task<IReadOnlyList<PendingNotificationJob>> ReadPendingAsync(
        int limit,
        DateTimeOffset? availableAtOrBefore = null,
        CancellationToken cancellationToken = default);
}

public sealed record PendingNotificationJob(
    Guid NotificationJobId,
    Guid EventId,
    Guid RecipientUserId,
    string Channel,
    string Title,
    string Message,
    string PayloadJson,
    int Attempts,
    DateTimeOffset AvailableAt,
    DateTimeOffset CreatedAt);
