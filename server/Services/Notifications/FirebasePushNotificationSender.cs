using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Poseidon.Server.Data;
using System.Text.Json;

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
        GoogleCredential credential = CreateCredential(options);

        AppOptions appOptions = new()
        {
            Credential = credential,
            ProjectId = string.IsNullOrWhiteSpace(options.ProjectId) ? null : options.ProjectId
        };

        FirebaseApp app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(appOptions);
        return FirebaseMessaging.GetMessaging(app);
    }

    private static GoogleCredential CreateCredential(FirebaseCloudMessagingOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.CredentialJson))
        {
            return GoogleCredential.FromJson(options.CredentialJson);
        }

        if (!string.IsNullOrWhiteSpace(options.ClientEmail) &&
            !string.IsNullOrWhiteSpace(options.PrivateKey))
        {
            string privateKey = options.PrivateKey.Replace("\\n", "\n", StringComparison.Ordinal);
            string projectId = options.ProjectId ?? string.Empty;
            var serviceAccount = new Dictionary<string, string>
            {
                ["type"] = "service_account",
                ["project_id"] = projectId,
                ["private_key_id"] = options.PrivateKeyId ?? string.Empty,
                ["private_key"] = privateKey,
                ["client_email"] = options.ClientEmail,
                ["client_id"] = options.ClientId ?? string.Empty,
                ["auth_uri"] = "https://accounts.google.com/o/oauth2/auth",
                ["token_uri"] = "https://oauth2.googleapis.com/token",
                ["auth_provider_x509_cert_url"] = "https://www.googleapis.com/oauth2/v1/certs",
                ["client_x509_cert_url"] = options.ClientX509CertUrl ?? string.Empty,
                ["universe_domain"] = "googleapis.com"
            };

            return GoogleCredential.FromJson(JsonSerializer.Serialize(serviceAccount));
        }

        return string.IsNullOrWhiteSpace(options.CredentialPath)
            ? GoogleCredential.GetApplicationDefault()
            : GoogleCredential.FromFile(options.CredentialPath);
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
    public string? CredentialJson { get; init; }
    public string? PrivateKeyId { get; init; }
    public string? PrivateKey { get; init; }
    public string? ClientEmail { get; init; }
    public string? ClientId { get; init; }
    public string? ClientX509CertUrl { get; init; }
}
