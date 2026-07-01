using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Poseidon.Server.Data;

namespace Poseidon.Server.Services.Notifications;

public interface IPushNotificationSender
{
    Task SendAsync(PushNotification notification, CancellationToken cancellationToken);
}

public sealed class NoOpPushNotificationSender(ILogger<NoOpPushNotificationSender> logger) : IPushNotificationSender
{
    public Task SendAsync(PushNotification notification, CancellationToken cancellationToken)
    {
        logger.LogDebug(
            "Skipping push notification {NotificationJobId}; Firebase Cloud Messaging is disabled.",
            notification.NotificationJobId);
        return Task.CompletedTask;
    }
}

public sealed class FirebasePushNotificationSender(
    IServiceScopeFactory scopeFactory,
    IOptions<FirebaseCloudMessagingOptions> options,
    ILogger<FirebasePushNotificationSender> logger) : IPushNotificationSender
{
    private readonly Lazy<FirebaseMessaging> messaging = new(() => CreateMessagingClient(options.Value));

    public async Task SendAsync(PushNotification notification, CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var deviceTokens = await dbContext.PushDeviceTokens
            .Where(token => token.UserId == notification.RecipientUserId &&
                token.Platform == "Android" &&
                token.RevokedAt == null)
            .Select(token => new { token.PushDeviceTokenId, token.Token })
            .ToListAsync(cancellationToken);

        if (deviceTokens.Count == 0)
        {
            logger.LogDebug(
                "No active Android push tokens found for notification {NotificationJobId}.",
                notification.NotificationJobId);
            return;
        }

        foreach (var deviceToken in deviceTokens)
        {
            try
            {
                string messageId = await messaging.Value.SendAsync(
                    new Message
                    {
                        Token = deviceToken.Token,
                        Notification = new FirebaseAdmin.Messaging.Notification
                        {
                            Title = notification.Title,
                            Body = notification.Message
                        },
                        Data = new Dictionary<string, string>
                        {
                            ["notificationJobId"] = notification.NotificationJobId.ToString(),
                            ["eventId"] = notification.EventId.ToString(),
                            ["channel"] = "Push"
                        },
                        Android = new AndroidConfig
                        {
                            Priority = Priority.High,
                            Notification = new AndroidNotification
                            {
                                ChannelId = "poseidon_events",
                                ClickAction = "FLUTTER_NOTIFICATION_CLICK"
                            }
                        }
                    },
                    cancellationToken);

                logger.LogInformation(
                    "Push notification {NotificationJobId} sent to token {PushDeviceTokenId}: {MessageId}",
                    notification.NotificationJobId,
                    deviceToken.PushDeviceTokenId,
                    messageId);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Push notification {NotificationJobId} failed for token {PushDeviceTokenId}.",
                    notification.NotificationJobId,
                    deviceToken.PushDeviceTokenId);
            }
        }
    }

    private static FirebaseMessaging CreateMessagingClient(FirebaseCloudMessagingOptions options)
    {
        GoogleCredential credential = string.IsNullOrWhiteSpace(options.CredentialPath)
            ? GoogleCredential.GetApplicationDefault()
            : GoogleCredential.FromFile(options.CredentialPath);

        AppOptions appOptions = new()
        {
            Credential = credential,
            ProjectId = string.IsNullOrWhiteSpace(options.ProjectId) ? null : options.ProjectId
        };

        FirebaseApp app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(appOptions);
        return FirebaseMessaging.GetMessaging(app);
    }
}

public sealed record PushNotification(
    Guid NotificationJobId,
    Guid EventId,
    Guid RecipientUserId,
    string Title,
    string Message,
    string PayloadJson);

public sealed class FirebaseCloudMessagingOptions
{
    public const string SectionName = "Firebase:CloudMessaging";

    public bool Enabled { get; init; }
    public string? ProjectId { get; init; }
    public string? CredentialPath { get; init; }
}
