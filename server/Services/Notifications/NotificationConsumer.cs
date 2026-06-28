using System.Text.Json;
using Microsoft.Extensions.Options;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Poseidon.Server.Services.Notifications;

public sealed class NotificationConsumer(
    IConfiguration configuration,
    IRabbitMqConnection rabbitMqConnection,
    IEmailNotificationSender emailSender,
    IOptions<RabbitMqOptions> options,
    ILogger<NotificationConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string connectionString = configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("ConnectionStrings:Default must be configured.");
    private readonly SemaphoreSlim consumerSemaphore = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        RabbitMqOptions rabbitOptions = options.Value;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IModel channel = rabbitMqConnection.CreateChannel();
                RabbitMqTopology.Declare(channel, rabbitOptions);
                channel.BasicQos(prefetchSize: 0, prefetchCount: rabbitOptions.ConsumerPrefetchCount, global: false);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += async (_, eventArgs) =>
                {
                    await HandleDeliveryAsync(channel, eventArgs, rabbitOptions, stoppingToken);
                };

                channel.BasicConsume(
                    queue: rabbitOptions.QueueName,
                    autoAck: false,
                    consumer: consumer);

                logger.LogInformation("Notification consumer started on queue {QueueName}.", rabbitOptions.QueueName);
                await WaitForChannelCloseAsync(channel, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Notification consumer failed while connecting or consuming.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, rabbitOptions.PublisherPollIntervalSeconds)), stoppingToken);
        }
    }

    private static async Task WaitForChannelCloseAsync(IModel channel, CancellationToken cancellationToken)
    {
        while (channel.IsOpen)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
    }

    private async Task HandleDeliveryAsync(
        IModel channel,
        BasicDeliverEventArgs eventArgs,
        RabbitMqOptions rabbitOptions,
        CancellationToken cancellationToken)
    {
        await consumerSemaphore.WaitAsync(cancellationToken);
        NotificationMessage? message = null;

        try
        {
            message = JsonSerializer.Deserialize<NotificationMessage>(eventArgs.Body.Span, JsonOptions);
            if (message is null || !string.Equals(message.Channel, "Email", StringComparison.OrdinalIgnoreCase))
            {
                channel.BasicReject(eventArgs.DeliveryTag, requeue: false);
                return;
            }

            if (await IsAlreadySucceededAsync(message.NotificationJobId, cancellationToken))
            {
                channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
                return;
            }

            EmailNotification notification = await CreateEmailNotificationAsync(message, cancellationToken);
            await emailSender.SendAsync(notification, cancellationToken);
            await MarkSucceededAsync(message, cancellationToken);
            channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
        }
        catch (Exception exception)
        {
            if (message is not null)
            {
                await MarkFailedOrRetryAsync(message, exception, rabbitOptions, cancellationToken);
                channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
            }
            else
            {
                channel.BasicReject(eventArgs.DeliveryTag, requeue: false);
            }

            logger.LogError(
                exception,
                "Failed to consume notification job {NotificationJobId}.",
                message?.NotificationJobId);
        }
        finally
        {
            consumerSemaphore.Release();
        }
    }

    private async Task<bool> IsAlreadySucceededAsync(Guid notificationJobId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM public.notification_jobs
                WHERE notification_job_id = @notificationJobId
                  AND job_status_id = 3
            );
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("notificationJobId", notificationJobId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private async Task<EmailNotification> CreateEmailNotificationAsync(
        NotificationMessage message,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT email
            FROM public.users
            WHERE user_id = @recipientUserId;
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("recipientUserId", message.RecipientUserId);

        string? recipientEmail = (string?)await command.ExecuteScalarAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            throw new InvalidOperationException($"Recipient user {message.RecipientUserId} was not found.");
        }

        return new EmailNotification(
            message.NotificationJobId,
            message.RecipientUserId,
            recipientEmail,
            message.Title,
            message.Message,
            message.PayloadJson);
    }

    private async Task MarkSucceededAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        const string lockSql = """
            SELECT job_status_id
            FROM public.notification_jobs
            WHERE notification_job_id = @notificationJobId
            FOR UPDATE;
            """;

        const string updateSql = """
            UPDATE public.notification_jobs
            SET job_status_id = 3,
                attempts = attempts + 1,
                processed_at = CURRENT_TIMESTAMP,
                publisher_locked_until = NULL,
                last_error = NULL
            WHERE notification_job_id = @notificationJobId
              AND job_status_id <> 3;
            """;

        const string deliverySql = """
            INSERT INTO public.notification_deliveries (
                notification_job_id,
                recipient_user_id,
                channel,
                result
            )
            SELECT @notificationJobId, @recipientUserId, @channel, 'Succeeded'
            WHERE NOT EXISTS (
                SELECT 1
                FROM public.notification_deliveries
                WHERE notification_job_id = @notificationJobId
                  AND result = 'Succeeded'
            );
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var lockCommand = new NpgsqlCommand(lockSql, connection, transaction))
        {
            lockCommand.Parameters.AddWithValue("notificationJobId", message.NotificationJobId);
            object? status = await lockCommand.ExecuteScalarAsync(cancellationToken);
            if (status is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return;
            }
        }

        await using (var updateCommand = new NpgsqlCommand(updateSql, connection, transaction))
        {
            updateCommand.Parameters.AddWithValue("notificationJobId", message.NotificationJobId);
            await updateCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var deliveryCommand = new NpgsqlCommand(deliverySql, connection, transaction))
        {
            deliveryCommand.Parameters.AddWithValue("notificationJobId", message.NotificationJobId);
            deliveryCommand.Parameters.AddWithValue("recipientUserId", message.RecipientUserId);
            deliveryCommand.Parameters.AddWithValue("channel", message.Channel);
            await deliveryCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task MarkFailedOrRetryAsync(
        NotificationMessage message,
        Exception exception,
        RabbitMqOptions rabbitOptions,
        CancellationToken cancellationToken)
    {
        int delaySeconds = 15;

        const string sql = """
            WITH current_job AS (
                SELECT attempts
                FROM public.notification_jobs
                WHERE notification_job_id = @notificationJobId
                FOR UPDATE
            ),
            updated_job AS (
                UPDATE public.notification_jobs AS job
                SET attempts = job.attempts + 1,
                    job_status_id = CASE
                        WHEN job.attempts + 1 >= @maxAttempts THEN 4
                        ELSE 1
                    END,
                    available_at = CURRENT_TIMESTAMP + (@delaySeconds * INTERVAL '1 second'),
                    published_at = NULL,
                    publisher_locked_until = NULL,
                    processed_at = CASE
                        WHEN job.attempts + 1 >= @maxAttempts THEN CURRENT_TIMESTAMP
                        ELSE NULL
                    END,
                    last_error = left(@error, 2000)
                WHERE job.notification_job_id = @notificationJobId
                  AND EXISTS (SELECT 1 FROM current_job)
                  AND job.job_status_id <> 3
                RETURNING job.notification_job_id
            )
            INSERT INTO public.notification_deliveries (
                notification_job_id,
                recipient_user_id,
                channel,
                result
            )
            SELECT @notificationJobId, @recipientUserId, @channel, 'Failed'
            WHERE EXISTS (SELECT 1 FROM updated_job);
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("notificationJobId", message.NotificationJobId);
        command.Parameters.AddWithValue("recipientUserId", message.RecipientUserId);
        command.Parameters.AddWithValue("channel", message.Channel);
        command.Parameters.AddWithValue("maxAttempts", Math.Clamp(rabbitOptions.MaxDeliveryAttempts, 1, 25));
        command.Parameters.AddWithValue("delaySeconds", delaySeconds);
        command.Parameters.AddWithValue("error", exception.Message);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
