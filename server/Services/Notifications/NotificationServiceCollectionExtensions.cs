using System.Net;
using Microsoft.Extensions.Options;

namespace Poseidon.Server.Services.Notifications;

public static class NotificationServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(options =>
                !options.Enabled ||
                !string.IsNullOrWhiteSpace(options.HostName) &&
                !string.IsNullOrWhiteSpace(options.UserName) &&
                !string.IsNullOrWhiteSpace(options.Password) &&
                !string.IsNullOrWhiteSpace(options.ExchangeName) &&
                !string.IsNullOrWhiteSpace(options.QueueName) &&
                !string.IsNullOrWhiteSpace(options.RoutingKey),
                "RabbitMq configuration is incomplete.")
            .Validate(options =>
                !options.Enabled ||
                options.ConsumerPrefetchCount > 0,
                "RabbitMq:ConsumerPrefetchCount must be greater than zero.")
            .Validate(options =>
                !options.Enabled ||
                !environment.IsProduction() ||
                !UsesDefaultCredentials(options),
                "RabbitMq production configuration must not use default or development credentials.")
            .Validate(options =>
                !options.Enabled ||
                !environment.IsProduction() ||
                options.RequireTls ||
                IsLocalOrPrivateHost(options.HostName),
                "RabbitMq production configuration must enable TLS unless the broker host is local or private.")
            .ValidateOnStart();

        services.AddSingleton<IEmailNotificationSender, LogEmailNotificationSender>();
        services.AddScoped<INotificationJobReader, NotificationJobReader>();
        services.AddScoped<INotificationJobProcessor, NotificationJobProcessor>();

        RabbitMqOptions rabbitMqOptions = configuration
            .GetSection(RabbitMqOptions.SectionName)
            .Get<RabbitMqOptions>() ?? new RabbitMqOptions();

        if (rabbitMqOptions.Enabled)
        {
            services.AddSingleton<IRabbitMqConnection, RabbitMqConnection>();
            services.AddHostedService<NotificationOutboxPublisher>();
            services.AddHostedService<NotificationConsumer>();
        }

        return services;
    }

    private static bool UsesDefaultCredentials(RabbitMqOptions options)
    {
        return string.Equals(options.UserName, "guest", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(options.Password, "guest", StringComparison.Ordinal) ||
            string.Equals(options.Password, "poseidon_dev_password", StringComparison.Ordinal);
    }

    private static bool IsLocalOrPrivateHost(string hostName)
    {
        if (string.Equals(hostName, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(hostName, out IPAddress? address))
        {
            return false;
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        byte[] bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return bytes[0] == 10 ||
                bytes[0] == 192 && bytes[1] == 168 ||
                bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31;
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal ||
                address.IsIPv6SiteLocal ||
                bytes[0] is 0xfc or 0xfd;
        }

        return false;
    }
}
