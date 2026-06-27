using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Poseidon.Server.Data;
using Poseidon.Server.Data.Entities;

namespace Poseidon.Server.Services;

public class RabbitMqConsumerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMqConsumerService> _logger;
    private IConnection? _connection;
    private IChannel? _channel; // Refactored from IModel to match v7 Client
    private const string QueueName = "poseidon_registration_queue";

    public RabbitMqConsumerService(IServiceProvider serviceProvider, ILogger<RabbitMqConsumerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        InitializeRabbitMq();
    }

    private void InitializeRabbitMq()
    {
        try
        {
            // Note: In local Docker setups, if the API runs inside a container, 
            // "localhost" might need to be "rabbitmq" (matching your docker-compose service name).
            var factory = new ConnectionFactory() { HostName = "localhost" };
            
            // Task-wrapped synchronous creation blocks matching v7 signatures safely
            var connectionTask = factory.CreateConnectionAsync();
            connectionTask.Wait();
            _connection = connectionTask.Result;

            var channelTask = _connection.CreateChannelAsync();
            channelTask.Wait();
            _channel = channelTask.Result;

            // WK-01: Synchronously assert the queue definition exists on broker initialization
            var queueDeclareTask = _channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
            queueDeclareTask.Wait();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RabbitMQ Broker connection initialization failed.");
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel == null)
        {
            _logger.LogError("Consumer executing context stopped: Channel initialization is missing.");
            return Task.CompletedTask;
        }

        stoppingToken.ThrowIfCancellationRequested();

        // WK-02: Setup the active asynchronous consumer loop matching the modern architecture pattern
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var messageString = Encoding.UTF8.GetString(body);
            
            // WK-07: Error Handling boundary wrapper
            try
            {
                using var jsonDocument = JsonDocument.Parse(messageString);
                var root = jsonDocument.RootElement;
                
                if (root.TryGetProperty("RegistrationId", out var regIdToken))
                {
                    Guid registrationId = regIdToken.GetGuid();
                    
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    // WK-08: Idempotency Guard - Prevent duplicate notification row creation
                    bool alreadyProcessed = await dbContext.Notifications
                        .AnyAsync(n => n.NotificationId == registrationId);

                    if (alreadyProcessed)
                    {
                        _logger.LogWarning("WK-08 Duplicate Guard triggered. Message {Id} skipped.", registrationId);
                        await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false); 
                        return;
                    }

                    string userEmail = root.TryGetProperty("UserEmail", out var emailProp) ? emailProp.GetString() ?? "" : "";
                    string eventTitle = root.TryGetProperty("EventTitle", out var titleProp) ? titleProp.GetString() ?? "" : "";
                    Guid userId = root.TryGetProperty("UserId", out var userProp) ? userProp.GetGuid() : Guid.Empty;
                    Guid eventId = root.TryGetProperty("EventId", out var eventProp) ? eventProp.GetGuid() : Guid.Empty;

                    Notification notificationEntity;

                    // Dynamically route and map structures matching WK-03, WK-04, and WK-05
                    if (root.TryGetProperty("WaitlistPosition", out var posToken))
                    {
                        // WK-04: Handle RegistrationWaitlistedMessage
                        int position = posToken.GetInt32();
                        notificationEntity = new Notification
                        {
                            NotificationId = registrationId, 
                            UserId = userId,
                            EventId = eventId,
                            NotificationType = "RegistrationWaitlisted",
                            RecipientEmail = userEmail,
                            MessageBody = $"<p>Hello! You have been placed on the waitlist for <strong>{eventTitle}</strong> at position #{position}.</p>",
                            IsSent = false,
                            RetryCount = 0
                        };
                    }
                    else if (messageString.Contains("Promoted") || messageString.Contains("WaitlistPromoted"))
                    {
                        // WK-05: Handle WaitlistPromotedMessage
                        notificationEntity = new Notification
                        {
                            NotificationId = registrationId,
                            UserId = userId,
                            EventId = eventId,
                            NotificationType = "WaitlistPromoted",
                            RecipientEmail = userEmail,
                            MessageBody = $"<p>Great news! You have been promoted out of the waitlist for <strong>{eventTitle}</strong>. Your seat is confirmed!</p>",
                            IsSent = false,
                            RetryCount = 0
                        };
                    }
                    else
                    {
                        // WK-03: Handle RegistrationConfirmedMessage
                        notificationEntity = new Notification
                        {
                            NotificationId = registrationId,
                            UserId = userId,
                            EventId = eventId,
                            NotificationType = "RegistrationConfirmed",
                            RecipientEmail = userEmail,
                            MessageBody = $"<p>Congratulations! Your seat reservation for <strong>{eventTitle}</strong> is locked in and confirmed.</p>",
                            IsSent = false,
                            RetryCount = 0
                        };
                    }

                    dbContext.Notifications.Add(notificationEntity);
                    await dbContext.SaveChangesAsync();
                }

                // WK-06: Send positive acknowledgment back to RabbitMQ asynchronously
                await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An execution error occurred parsing message payload.");
                
                // WK-07: Negatively acknowledge message and tell broker to requeue it for another attempt
                await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        // Fire the task and let it await internal registration processing natively
        _channel.BasicConsumeAsync(queue: QueueName, autoAck: false, consumer: consumer).Wait();
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        try
        {
            // Close the channel and connection asynchronously before letting the base dispose run
            _channel?.CloseAsync().Wait();
            _connection?.CloseAsync().Wait();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while closing RabbitMQ connections during worker disposal.");
        }
        finally
        {
            base.Dispose();
        }
    }
}