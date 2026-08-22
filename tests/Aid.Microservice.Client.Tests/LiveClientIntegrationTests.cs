using Aid.Microservice.Client.AspNetCore;
using Aid.Microservice.Client.Tests.TestContracts;
using Aid.Microservice.Generated;
using Aid.Microservice.Shared;
using Aid.Microservice.Shared.Attributes;
using Aid.Microservice.Shared.Configuration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aid.Microservice.Client.Tests;

[Microservice("calc")]
public class ServerCalculatorService
{
    [RpcCallable("add")]
    public int Add(int a, int b) => a + b;

    [RpcCallable("async_square")]
    public async Task<int> AsyncSquare(int value)
    {
        await Task.Yield();
        return value * value;
    }
}

[MicroserviceQuery("calc_tax")]
public class ServerTaxQueryHandler
{
    public async Task<TaxResult> HandleAsync(TaxRequest request, CancellationToken cancellationToken)
    {
        await Task.Yield();
        var tax = request.Amount * request.Rate;
        return new TaxResult(request.Amount + tax, tax);
    }
}

public class LiveClientIntegrationTests : IAsyncLifetime
{
    private IHost? _serverHost;
    private ServiceProvider? _clientServiceProvider;
    private bool _rabbitAvailable;

    public async Task InitializeAsync()
    {
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

        // 1. Build and start server
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

                services.AddAidMicroserviceGenerated();
            })
            .Build();

        await _serverHost.StartAsync();
        await Task.Delay(1000);

        // 2. Build client DI container with typed generated clients
        var clientServices = new ServiceCollection();
        clientServices.AddLogging();
        clientServices.AddAidMicroserviceClient(opts =>
        {
            opts.Hostname = rabbitConfig.Hostname;
            opts.Port = rabbitConfig.Port;
            opts.Username = rabbitConfig.Username;
            opts.Password = rabbitConfig.Password;
        });

        // Register source-generated typed client proxies
        clientServices.AddAidMicroserviceGeneratedClients();

        _clientServiceProvider = clientServices.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        if (_clientServiceProvider != null)
        {
            await _clientServiceProvider.DisposeAsync();
        }

        if (_serverHost != null)
        {
            await _serverHost.StopAsync();
            _serverHost.Dispose();
        }
    }

    [Fact]
    public async Task TypedClient_Add_ReturnsCalculatedResult()
    {
        if (!_rabbitAvailable) return;

        var calcClient = _clientServiceProvider!.GetRequiredService<ICalculatorRpcClient>();
        var result = await calcClient.Add(35, 7);

        result.Should().Be(42);
    }

    [Fact]
    public async Task TypedClient_AsyncSquare_ReturnsCalculatedResult()
    {
        if (!_rabbitAvailable) return;

        var calcClient = _clientServiceProvider!.GetRequiredService<ICalculatorRpcClient>();
        var result = await calcClient.AsyncSquare(9);

        result.Should().Be(81);
    }

    [Fact]
    public async Task TypedClient_Query_ReturnsCalculatedDto()
    {
        if (!_rabbitAvailable) return;

        var taxClient = _clientServiceProvider!.GetRequiredService<ITaxRpcClient>();
        var result = await taxClient.CalculateTax(new TaxRequest(500m, 0.20m, "FR"));

        result.Should().NotBeNull();
        result.Tax.Should().Be(100m);
        result.Total.Should().Be(600m);
    }
}
