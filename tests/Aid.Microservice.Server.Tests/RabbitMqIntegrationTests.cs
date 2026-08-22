using Aid.Microservice.Client;
using Aid.Microservice.Client.Infrastructure;
using Aid.Microservice.Generated;
using Aid.Microservice.Server.Tests.TestServices;
using Aid.Microservice.Shared;
using Aid.Microservice.Shared.Configuration;
using Aid.Microservice.Shared.Interfaces;
using Aid.Microservice.Shared.Protocols;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aid.Microservice.Server.Tests;

public class RabbitMqIntegrationTests : IAsyncLifetime
{
    private IHost? _serverHost;
    private IRpcClientFactory? _clientFactory;
    private bool _rabbitAvailable;

    public async Task InitializeAsync()
    {
        // Try connecting to local RabbitMQ
        var rabbitConfig = new RabbitMqConfiguration
        {
            Hostname = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost",
            Port = int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out var p) ? p : 5672,
            Username = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest",
            Password = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "guest",
            DeleteQueuesOnShutdown = true,
            DeleteExchangesOnShutdown = true
        };

        var connService = new RabbitMqConnectionService(
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<RabbitMqConnectionService>(),
            Options.Create(rabbitConfig)
        );

        _rabbitAvailable = await connService.TryConnectAsync(CancellationToken.None);
        await connService.DisposeAsync();

        if (!_rabbitAvailable)
        {
            return;
        }

        // Build and start server host with generated endpoints
        _serverHost = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.Configure<RabbitMqConfiguration>(opts =>
                {
                    opts.Hostname = rabbitConfig.Hostname;
                    opts.Port = rabbitConfig.Port;
                    opts.Username = rabbitConfig.Username;
                    opts.Password = rabbitConfig.Password;
                    opts.DeleteQueuesOnShutdown = true;
                    opts.DeleteExchangesOnShutdown = true;
                });

                // Source generated registration
                services.AddAidMicroserviceGenerated();
            })
            .Build();

        await _serverHost.StartAsync();

        // Give background listeners a moment to declare exchanges and bind queues
        await Task.Delay(1000);

        // Setup Client Factory
        var clientServices = new ServiceCollection();
        clientServices.AddLogging();
        clientServices.Configure<RabbitMqConfiguration>(opts =>
        {
            opts.Hostname = rabbitConfig.Hostname;
            opts.Port = rabbitConfig.Port;
            opts.Username = rabbitConfig.Username;
            opts.Password = rabbitConfig.Password;
        });
        clientServices.AddSingleton<IRabbitMqConnectionService, RabbitMqConnectionService>();
        clientServices.AddSingleton<IRpcProtocol, DefaultJsonProtocol>();
        clientServices.AddSingleton<IRpcClientFactory, RpcClientFactory>();

        var clientSp = clientServices.BuildServiceProvider();
        _clientFactory = clientSp.GetRequiredService<IRpcClientFactory>();
    }

    public async Task DisposeAsync()
    {
        if (_serverHost != null)
        {
            await _serverHost.StopAsync();
            _serverHost.Dispose();
        }
    }

    [Fact]
    public async Task LiveRabbitMq_SyncRpcCall_CalculatesCorrectly()
    {
        if (!_rabbitAvailable) return;

        var client = _clientFactory!.CreateClient("calc");
        var result = await client.CallAsync<int>("add", new { a = 12, b = 30 });

        result.Should().Be(42);
    }

    [Fact]
    public async Task LiveRabbitMq_AsyncRpcCall_CalculatesCorrectly()
    {
        if (!_rabbitAvailable) return;

        var client = _clientFactory!.CreateClient("calc");
        var result = await client.CallAsync<int>("async_square", new { value = 8 });

        result.Should().Be(64);
    }

    [Fact]
    public async Task LiveRabbitMq_MicroserviceQueryCall_ReturnsDto()
    {
        if (!_rabbitAvailable) return;

        var client = _clientFactory!.CreateClient("query_calc_tax");
        var result = await client.CallQueryAsync<TaxResult>("calc_tax", new TaxRequest(200m, 0.15m, "DE"));

        result.Should().NotBeNull();
        result.Tax.Should().Be(30m);
        result.Total.Should().Be(230m);
    }
}
