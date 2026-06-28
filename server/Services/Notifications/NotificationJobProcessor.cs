using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Server.Data;
using Poseidon.Server.Data.Entities;

namespace Poseidon.Server.Services.Notifications;

public sealed class NotificationJobProcessor(
    AppDbContext dbContext,
    IEmailNotificationSender emailSender) : INotificationJobProcessor
{
    private const int PendingStatusId = 1;
    private const int SucceededStatusId = 3;
    private const int FailedStatusId = 4;
    private const string RegistrationConfirmedType = "RegistrationConfirmed";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<NotificationJobProcessResult> ProcessAsync(
        Guid notificationJobId,
        int maxAttempts = 5,
        CancellationToken cancellationToken = default)
    {
        NotificationJob? job = await dbContext.NotificationJobs
            .Include(notificationJob => notificationJob.RecipientUser)
            .SingleOrDefaultAsync(
                notificationJob => notificationJob.NotificationJobId == notificationJobId,
                cancellationToken);

        if (job is null)
        {
            return new NotificationJobProcessResult(NotificationJobProcessStatus.NotFound);
        }

        if (job.JobStatusId != PendingStatusId)
        {
            return new NotificationJobProcessResult(NotificationJobProcessStatus.NotPending);
        }

        if (!IsRegistrationConfirmedPayload(job))
        {
            return new NotificationJobProcessResult(NotificationJobProcessStatus.UnsupportedType);
        }

        string? recipientEmail = job.RecipientUser?.Email;
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            return await MarkFailedAsync(
                job,
                "Recipient email was not found.",
                maxAttempts,
                cancellationToken);
        }

        try
        {
            await emailSender.SendAsync(
                new EmailNotification(
                    job.NotificationJobId,
                    job.RecipientUserId,
                    recipientEmail,
                    job.Title,
                    job.Message,
                    job.Payload),
                cancellationToken);

            await MarkSucceededAsync(job, cancellationToken);
            return new NotificationJobProcessResult(NotificationJobProcessStatus.Succeeded);
        }
        catch (Exception exception)
        {
            return await MarkFailedAsync(job, exception.Message, maxAttempts, cancellationToken);
        }
    }

    private static bool IsRegistrationConfirmedPayload(NotificationJob job)
    {
        try
        {
            RegistrationConfirmedPayload? payload = JsonSerializer.Deserialize<RegistrationConfirmedPayload>(
                job.Payload,
                JsonOptions);

            return payload is not null &&
                string.Equals(payload.Type, RegistrationConfirmedType, StringComparison.Ordinal) &&
                payload.EventId == job.EventId &&
                payload.StudentId == job.RecipientUserId;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task MarkSucceededAsync(NotificationJob job, CancellationToken cancellationToken)
    {
        job.JobStatusId = SucceededStatusId;
        job.Attempts++;
        job.ProcessedAt = DateTimeOffset.UtcNow;
        job.PublisherLockedUntil = null;
        job.LastError = null;

        bool deliveryExists = await dbContext.NotificationDeliveries.AnyAsync(
            delivery => delivery.NotificationJobId == job.NotificationJobId &&
                delivery.Result == "Succeeded",
            cancellationToken);

        if (!deliveryExists)
        {
            dbContext.NotificationDeliveries.Add(new NotificationDelivery
            {
                NotificationJobId = job.NotificationJobId,
                RecipientUserId = job.RecipientUserId,
                Channel = job.Channel,
                Result = "Succeeded"
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<NotificationJobProcessResult> MarkFailedAsync(
        NotificationJob job,
        string error,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        int boundedMaxAttempts = Math.Clamp(maxAttempts, 1, 25);

        job.Attempts++;
        job.JobStatusId = job.Attempts >= boundedMaxAttempts ? FailedStatusId : PendingStatusId;
        job.AvailableAt = DateTimeOffset.UtcNow.AddSeconds(15);
        job.PublishedAt = null;
        job.PublisherLockedUntil = null;
        job.ProcessedAt = job.JobStatusId == FailedStatusId ? DateTimeOffset.UtcNow : null;
        job.LastError = error.Length > 2000 ? error[..2000] : error;

        dbContext.NotificationDeliveries.Add(new NotificationDelivery
        {
            NotificationJobId = job.NotificationJobId,
            RecipientUserId = job.RecipientUserId,
            Channel = job.Channel,
            Result = "Failed"
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return new NotificationJobProcessResult(NotificationJobProcessStatus.Failed, error);
    }

    private sealed record RegistrationConfirmedPayload(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("event_id")] Guid EventId,
        [property: JsonPropertyName("student_id")] Guid StudentId);
}
