using RabbitMQ.Client;

namespace Aid.Microservice.Shared;

public interface IRabbitMqConnectionService : IAsyncDisposable
{
    bool IsConnected { get; }
    Task<bool> TryConnectAsync(CancellationToken cancellationToken);
    Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken);
}