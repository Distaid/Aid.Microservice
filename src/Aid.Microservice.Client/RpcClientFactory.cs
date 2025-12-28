using System.Collections.Concurrent;
using Aid.Microservice.Client.Infrastructure;
using Aid.Microservice.Shared;
using Aid.Microservice.Shared.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Aid.Microservice.Client;

public class RpcClientFactory : IRpcClientFactory, IAsyncDisposable
{
    private readonly IRabbitMqConnectionService _connectionService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly bool _ownsConnectionService;
    private readonly string _exchangeName;
    
    private readonly ConcurrentDictionary<string, IRpcClient> _clients = new(StringComparer.OrdinalIgnoreCase);
    
    public RpcClientFactory(
        IRabbitMqConnectionService connectionService, 
        ILoggerFactory loggerFactory,
        IOptions<RabbitMqConfiguration> config)
    {
        _connectionService = connectionService;
        _loggerFactory = loggerFactory;
        _ownsConnectionService = false;
        
        _exchangeName = !string.IsNullOrWhiteSpace(config.Value.ExchangeName) 
            ? config.Value.ExchangeName 
            : "aid_rpc";
    }

    public RpcClientFactory(RabbitMqConfiguration configuration)
    {
        _loggerFactory = NullLoggerFactory.Instance;
        var connectionLogger = _loggerFactory.CreateLogger<RabbitMqConnectionService>();
        
        _connectionService = new RabbitMqConnectionService(
            connectionLogger, 
            Options.Create(configuration));

        _ownsConnectionService = true;
        _exchangeName = configuration.ExchangeName;
    }
    
    public RpcClientFactory(string hostname, int port, string username, string password, string exchangeName = "aid_rpc", int retryCount = 3, int recoveryInterval = 3)
    {
        var config = new RabbitMqConfiguration
        {
            Hostname = hostname,
            Port = port,
            Username = username,
            Password = password,
            ExchangeName = exchangeName,
            RetryCount = retryCount,
            RecoveryInterval = recoveryInterval
        };

        _loggerFactory = NullLoggerFactory.Instance;
        var connectionLogger = _loggerFactory.CreateLogger<RabbitMqConnectionService>();
        
        _connectionService = new RabbitMqConnectionService(
            connectionLogger, 
            Options.Create(config));

        _ownsConnectionService = true;
        _exchangeName = exchangeName;
    }
    
    public IRpcClient CreateClient(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException("Service name must be specified", nameof(serviceName));
        }
        
        return _clients.GetOrAdd(serviceName, name => new RpcClient(
            _connectionService, 
            _loggerFactory.CreateLogger<RpcClient>(), 
            name,
            _exchangeName));
    }
    
    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients.Values)
        {
            try
            {
                await client.DisposeAsync();
            }
            catch
            {
                // ignore
            }
        }
        _clients.Clear();

        if (_ownsConnectionService)
        {
            await _connectionService.DisposeAsync();
        }
        
        GC.SuppressFinalize(this);
    }
}