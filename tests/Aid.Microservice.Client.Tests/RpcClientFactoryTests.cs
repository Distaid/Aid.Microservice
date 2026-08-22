using Aid.Microservice.Shared.Configuration;
using Aid.Microservice.Shared.Interfaces;
using Aid.Microservice.Shared.Protocols;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aid.Microservice.Client.Tests;

public class RpcClientFactoryTests
{
    private class DummyConnectionService : IRabbitMqConnectionService
    {
        public bool TryConnectCalled { get; private set; }
        public bool IsConnected => true;

        public Task<bool> TryConnectAsync(CancellationToken token = default)
        {
            TryConnectCalled = true;
            return Task.FromResult(true);
        }

        public Task<RabbitMQ.Client.IChannel> CreateChannelAsync(CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public void CreateClient_Throws_WhenServiceNameIsEmpty()
    {
        var factory = new RpcClientFactory(
            new DummyConnectionService(),
            NullLoggerFactory.Instance,
            Options.Create(new RabbitMqConfiguration()),
            new DefaultJsonProtocol());

        var act = () => factory.CreateClient("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task CreateClient_ReturnsSameInstance_WhenCalledMultipleTimes()
    {
        var factory = new RpcClientFactory(
            new DummyConnectionService(),
            NullLoggerFactory.Instance,
            Options.Create(new RabbitMqConfiguration()),
            new DefaultJsonProtocol());

        var client1 = factory.CreateClient("test-service");
        var client2 = factory.CreateClient("test-service");

        client1.Should().BeSameAs(client2);

        await client1.DisposeAsync();
        await client2.DisposeAsync();
        await factory.DisposeAsync();
    }
}
