using RabbitMQ.Client;

namespace Poseidon.Server.Services.Notifications;

internal static class RabbitMqTopology
{
    public static void Declare(IModel channel, RabbitMqOptions options)
    {
        channel.ExchangeDeclare(
            exchange: options.ExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false);

        channel.QueueDeclare(
            queue: options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false);

        channel.QueueBind(
            queue: options.QueueName,
            exchange: options.ExchangeName,
            routingKey: options.RoutingKey);
    }
}
