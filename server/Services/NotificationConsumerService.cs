using Microsoft.EntityFrameworkCore;
using FluentEmail.Core;
using Poseidon.Server.Data;

namespace Poseidon.Server.Services;

public class NotificationConsumerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationConsumerService> _logger;

    public NotificationConsumerService(IServiceProvider serviceProvider, ILogger<NotificationConsumerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification Background Consumer Service successfully deployed and initialized.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                // Request AppDbContext directly matching your project structure
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var fluentEmail = scope.ServiceProvider.GetRequiredService<IFluentEmail>();

                // Fetch pending notifications straight from the context
                var pendingList = await dbContext.Notifications
                    .Where(n => !n.IsSent && n.RetryCount < 3)
                    .ToListAsync(stoppingToken);

                foreach (var notification in pendingList)
                {
                    _logger.LogInformation("Processing {Type} for {Email}", notification.NotificationType, notification.RecipientEmail);

                    string subject = notification.NotificationType switch
                    {
                        "RegistrationConfirmed" => "Spot Confirmed!",
                        "RegistrationWaitlisted" => "Joined the Waitlist",
                        "WaitlistPromoted" => "Good news: You are off the Waitlist!",
                        _ => "Poseidon Event Update"
                    };

                    var emailToSend = fluentEmail
                        .To(notification.RecipientEmail)
                        .Subject(subject)
                        .Body(notification.MessageBody, isHtml: true);

                    var result = await emailToSend.SendAsync(stoppingToken);

                    if (result.Successful)
                    {
                        notification.IsSent = true;
                        notification.SentAt = DateTimeOffset.UtcNow;
                    }
                    else
                    {
                        notification.RetryCount++;
                        notification.FailureReason = string.Join("; ", result.ErrorMessages);
                        _logger.LogWarning("Email delivery failed for notification {Id}. Error: {Reason}", notification.NotificationId, notification.FailureReason);
                    }

                    // Save updates straight back to the database context
                    await dbContext.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception error occurred within the consumer processing loop.");
            }

            // Poll every 10 seconds
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}       