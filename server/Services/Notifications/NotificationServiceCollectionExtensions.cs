using Microsoft.Extensions.Options;

namespace Poseidon.Server.Services.Notifications;

public static class NotificationServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(options =>
                !string.IsNullOrWhiteSpace(options.HostName) &&
                !string.IsNullOrWhiteSpace(options.UserName) &&
                !string.IsNullOrWhiteSpace(options.Password) &&
                !string.IsNullOrWhiteSpace(options.ExchangeName) &&
                !string.IsNullOrWhiteSpace(options.QueueName) &&
                !string.IsNullOrWhiteSpace(options.RoutingKey),
                "RabbitMq configuration is incomplete.")
            .ValidateOnStart();

        services.AddSingleton<IRabbitMqConnection, RabbitMqConnection>();
        services.AddSingleton<IEmailNotificationSender, LogEmailNotificationSender>();
        services.AddHostedService<NotificationOutboxPublisher>();
        services.AddHostedService<NotificationConsumer>();

        return services;
    }
}
