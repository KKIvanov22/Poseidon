using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Npgsql;
using RabbitMQ.Client;

namespace Poseidon.Server.Services.Notifications;

public sealed class NotificationOutboxPublisher(
    IConfiguration configuration,
    IRabbitMqConnection rabbitMqConnection,
    IOptions<RabbitMqOptions> options,
    ILogger<NotificationOutboxPublisher> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string connectionString = configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("ConnectionStrings:Default must be configured.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        RabbitMqOptions rabbitOptions = options.Value;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                IReadOnlyList<NotificationMessage> jobs = await ClaimJobsAsync(rabbitOptions, stoppingToken);
                foreach (NotificationMessage job in jobs)
                {
                    await PublishAsync(job, rabbitOptions, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Notification outbox publisher failed while polling or publishing.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, rabbitOptions.PublisherPollIntervalSeconds)), stoppingToken);
        }
    }

    private async Task<IReadOnlyList<NotificationMessage>> ClaimJobsAsync(
        RabbitMqOptions rabbitOptions,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH claimable AS (
                SELECT notification_job_id
                FROM public.notification_jobs
                WHERE (
                        job_status_id = 1
                        AND available_at <= CURRENT_TIMESTAMP
                    )
                    OR (
                        job_status_id = 2
                        AND published_at IS NULL
                        AND publisher_locked_until <= CURRENT_TIMESTAMP
                    )
                ORDER BY created_at
                LIMIT @batchSize
                FOR UPDATE SKIP LOCKED
            )
            UPDATE public.notification_jobs AS job
            SET job_status_id = 2,
                publisher_locked_until = CURRENT_TIMESTAMP + (@leaseSeconds * INTERVAL '1 second'),
                last_error = NULL
            FROM claimable
            WHERE job.notification_job_id = claimable.notification_job_id
            RETURNING job.notification_job_id,
                      job.event_id,
                      job.recipient_user_id,
                      job.channel,
                      job.title,
                      job.message,
                      job.payload::text;
            """;

        var jobs = new List<NotificationMessage>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("batchSize", Math.Clamp(rabbitOptions.PublisherBatchSize, 1, 100));
        command.Parameters.AddWithValue("leaseSeconds", Math.Clamp(rabbitOptions.PublisherLeaseSeconds, 15, 600));

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            jobs.Add(new NotificationMessage(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6)));
        }

        await reader.CloseAsync();
        await transaction.CommitAsync(cancellationToken);

        return jobs;
    }

    private async Task PublishAsync(
        NotificationMessage message,
        RabbitMqOptions rabbitOptions,
        CancellationToken cancellationToken)
    {
        try
        {
            using IModel channel = rabbitMqConnection.CreateChannel();
            RabbitMqTopology.Declare(channel, rabbitOptions);
            channel.ConfirmSelect();

            byte[] body = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
            IBasicProperties properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Type = nameof(NotificationMessage);
            properties.MessageId = message.NotificationJobId.ToString();
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            channel.BasicPublish(
                exchange: rabbitOptions.ExchangeName,
                routingKey: rabbitOptions.RoutingKey,
                mandatory: true,
                basicProperties: properties,
                body: body);

            channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));
            await MarkPublishedAsync(message.NotificationJobId, cancellationToken);

            logger.LogInformation(
                "Published notification job {NotificationJobId} to RabbitMQ.",
                message.NotificationJobId);
        }
        catch (Exception exception)
        {
            await ReleaseForRetryAsync(message.NotificationJobId, exception.Message, cancellationToken);
            logger.LogError(
                exception,
                "Failed to publish notification job {NotificationJobId}.",
                message.NotificationJobId);
        }
    }

    private async Task MarkPublishedAsync(Guid notificationJobId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE public.notification_jobs
            SET published_at = CURRENT_TIMESTAMP,
                publisher_locked_until = NULL
            WHERE notification_job_id = @notificationJobId;
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("notificationJobId", notificationJobId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task ReleaseForRetryAsync(Guid notificationJobId, string error, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE public.notification_jobs
            SET job_status_id = 1,
                available_at = CURRENT_TIMESTAMP + INTERVAL '30 seconds',
                publisher_locked_until = NULL,
                last_error = left(@error, 2000)
            WHERE notification_job_id = @notificationJobId
              AND job_status_id = 2
              AND published_at IS NULL;
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("notificationJobId", notificationJobId);
        command.Parameters.AddWithValue("error", error);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
