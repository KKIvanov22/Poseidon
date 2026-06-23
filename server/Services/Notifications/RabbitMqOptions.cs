namespace Poseidon.Server.Services.Notifications;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string UserName { get; init; } = "guest";
    public string Password { get; init; } = "guest";
    public string VirtualHost { get; init; } = "/";
    public string ExchangeName { get; init; } = "poseidon.notifications";
    public string QueueName { get; init; } = "poseidon.notifications.email";
    public string RoutingKey { get; init; } = "notifications.email";
    public int PublisherPollIntervalSeconds { get; init; } = 5;
    public int PublisherBatchSize { get; init; } = 25;
    public int PublisherLeaseSeconds { get; init; } = 60;
    public ushort ConsumerPrefetchCount { get; init; } = 10;
    public int MaxDeliveryAttempts { get; init; } = 5;
}
