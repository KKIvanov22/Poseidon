namespace Poseidon.Server.Services.Notifications;

public sealed record NotificationMessage(
    Guid NotificationJobId,
    Guid EventId,
    Guid RecipientUserId,
    string Channel,
    string Title,
    string Message,
    string PayloadJson);

public sealed record EmailNotification(
    Guid NotificationJobId,
    Guid EventId,
    Guid RecipientUserId,
    string RecipientEmail,
    string Title,
    string Message,
    string PayloadJson);
