using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Poseidon.Server.Services.Notifications;

public interface IRabbitMqConnection : IDisposable
{
    IModel CreateChannel();
}

public sealed class RabbitMqConnection(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqConnection> logger) : IRabbitMqConnection
{
    private readonly object syncRoot = new();
    private IConnection? connection;
    private bool disposed;

    public IModel CreateChannel()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(RabbitMqConnection));
        }

        EnsureConnected();
        return connection!.CreateModel();
    }

    public void Dispose()
    {
        disposed = true;
        connection?.Dispose();
    }

    private void EnsureConnected()
    {
        if (connection?.IsOpen == true)
        {
            return;
        }

        lock (syncRoot)
        {
            if (connection?.IsOpen == true)
            {
                return;
            }

            RabbitMqOptions rabbitOptions = options.Value;
            Validate(rabbitOptions);

            var factory = new ConnectionFactory
            {
                HostName = rabbitOptions.HostName,
                Port = rabbitOptions.Port,
                UserName = rabbitOptions.UserName,
                Password = rabbitOptions.Password,
                VirtualHost = rabbitOptions.VirtualHost,
                Ssl = new SslOption
                {
                    Enabled = rabbitOptions.RequireTls,
                    ServerName = string.IsNullOrWhiteSpace(rabbitOptions.TlsServerName)
                        ? rabbitOptions.HostName
                        : rabbitOptions.TlsServerName
                },
                DispatchConsumersAsync = true,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                ClientProvidedName = "poseidon-server"
            };

            connection = factory.CreateConnection();
            logger.LogInformation("Connected to RabbitMQ at {HostName}:{Port}.", rabbitOptions.HostName, rabbitOptions.Port);
        }
    }

    private static void Validate(RabbitMqOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.HostName) ||
            string.IsNullOrWhiteSpace(options.UserName) ||
            string.IsNullOrWhiteSpace(options.Password) ||
            string.IsNullOrWhiteSpace(options.ExchangeName) ||
            string.IsNullOrWhiteSpace(options.QueueName) ||
            string.IsNullOrWhiteSpace(options.RoutingKey))
        {
            throw new InvalidOperationException("RabbitMq configuration is incomplete.");
        }
    }
}
