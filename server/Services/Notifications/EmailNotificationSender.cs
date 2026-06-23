namespace Poseidon.Server.Services.Notifications;

public interface IEmailNotificationSender
{
    Task SendAsync(EmailNotification notification, CancellationToken cancellationToken);
}

public sealed class LogEmailNotificationSender(ILogger<LogEmailNotificationSender> logger) : IEmailNotificationSender
{
    public Task SendAsync(EmailNotification notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Email notification {NotificationJobId} for user {RecipientUserId} would be sent to {RecipientEmail}: {Title}",
            notification.NotificationJobId,
            notification.RecipientUserId,
            notification.RecipientEmail,
            notification.Title);

        return Task.CompletedTask;
    }
}
