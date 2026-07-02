using System.Net.Http.Headers;
using System.Net.Http.Json;
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

public sealed class BrevoEmailNotificationSender(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    IPushNotificationSender pushNotificationSender,
    ILogger<BrevoEmailNotificationSender> logger) : IEmailNotificationSender
{
    private const string ClientName = "BrevoEmail";

    public async Task SendAsync(EmailNotification notification, CancellationToken cancellationToken)
    {
        string apiKey = configuration["Brevo:ApiKey"] ??
            throw new InvalidOperationException("Brevo:ApiKey must be configured.");
        string senderEmail = configuration["Brevo:FromEmail"] ??
            configuration["Smtp:FromEmail"] ??
            "no-reply@poseidon.com";
        string senderName = configuration["Brevo:FromName"] ??
            configuration["Smtp:FromName"] ??
            "Poseidon Events System";

        using HttpRequestMessage request = new(HttpMethod.Post, "/v3/smtp/email");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("api-key", apiKey);
        request.Content = JsonContent.Create(new
        {
            sender = new
            {
                name = senderName,
                email = senderEmail
            },
            to = new[]
            {
                new
                {
                    email = notification.RecipientEmail
                }
            },
            subject = notification.Title,
            textContent = notification.Message
        });

        HttpClient httpClient = httpClientFactory.CreateClient(ClientName);
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning(
                "Brevo email notification {NotificationJobId} failed for {RecipientEmail}: {StatusCode} {ResponseBody}",
                notification.NotificationJobId,
                notification.RecipientEmail,
                response.StatusCode,
                responseBody);

            throw new InvalidOperationException(
                $"Brevo email notification failed: {(int)response.StatusCode} {responseBody}");
        }

        logger.LogInformation(
            "Brevo email notification {NotificationJobId} sent to {RecipientEmail}.",
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
                "Push fan-out failed after Brevo email notification {NotificationJobId} was sent.",
                notification.NotificationJobId);
        }
    }
}
