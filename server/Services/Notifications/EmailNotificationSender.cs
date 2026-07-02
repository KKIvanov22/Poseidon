using FluentEmail.Core;
using FluentEmail.Core.Models;

namespace Poseidon.Server.Services.Notifications;

public interface IEmailNotificationSender
{
    Task SendAsync(EmailNotification notification, CancellationToken cancellationToken);
}

public sealed class SmtpEmailNotificationSender(
    IServiceScopeFactory scopeFactory,
    IPushNotificationSender pushNotificationSender,
    ILogger<SmtpEmailNotificationSender> logger) : IEmailNotificationSender
{
    public async Task SendAsync(EmailNotification notification, CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IFluentEmailFactory emailFactory = scope.ServiceProvider.GetRequiredService<IFluentEmailFactory>();

        SendResponse response = await emailFactory
            .Create()
            .To(notification.RecipientEmail)
            .Subject(notification.Title)
            .Body(notification.Message, isHtml: false)
            .SendAsync(cancellationToken);

        if (!response.Successful)
        {
            string error = string.Join("; ", response.ErrorMessages);
            logger.LogWarning(
                "Email notification {NotificationJobId} failed for {RecipientEmail}: {Error}",
                notification.NotificationJobId,
                notification.RecipientEmail,
                error);

            throw new InvalidOperationException($"Email notification failed: {error}");
        }

        logger.LogInformation(
            "Email notification {NotificationJobId} sent to {RecipientEmail}.",
            notification.NotificationJobId,
            notification.RecipientEmail);

        try
        {
            await pushNotificationSender.SendAsync(
                new PushNotification(
                    notification.NotificationJobId,
                    notification.EventId,
                    notification.RecipientUserId,
                    notification.Title,
                    notification.Message,
                    notification.PayloadJson),
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Push fan-out failed after email notification {NotificationJobId} was sent.",
                notification.NotificationJobId);
        }
    }
}
