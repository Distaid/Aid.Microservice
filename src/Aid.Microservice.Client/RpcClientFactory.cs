using System.Collections.Concurrent;
using Aid.Microservice.Client.Infrastructure;
using Aid.Microservice.Shared;
using Aid.Microservice.Shared.Configuration;
using Aid.Microservice.Shared.Interfaces;
using Aid.Microservice.Shared.Protocols;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Aid.Microservice.Client;

public class RpcClientFactory : IRpcClientFactory, IAsyncDisposable
{
    private readonly IRabbitMqConnectionService _connectionService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IRpcProtocol _protocol;
    private readonly bool _ownsConnectionService;
    private readonly string _exchangeName;
    
    private readonly ConcurrentDictionary<string, IRpcClient> _clients = new(StringComparer.OrdinalIgnoreCase);
    
    public RpcClientFactory(
        IRabbitMqConnectionService connectionService, 
        ILoggerFactory loggerFactory,
        IOptions<RabbitMqConfiguration> config,
        IRpcProtocol protocol)
    {
        _connectionService = connectionService;
        _loggerFactory = loggerFactory;
        _ownsConnectionService = false;
        _exchangeName = config.Value.ExchangeName;
        _protocol = protocol;
    }

    public RpcClientFactory(RabbitMqConfiguration configuration, IRpcProtocol? protocol = null)
    {
        _loggerFactory = NullLoggerFactory.Instance;
        var connectionLogger = _loggerFactory.CreateLogger<RabbitMqConnectionService>();
        
        _connectionService = new RabbitMqConnectionService(
            connectionLogger, 
            Options.Create(configuration));

        _protocol = protocol ?? new DefaultJsonProtocol();
        _exchangeName = string.IsNullOrEmpty(configuration.ExchangeName)
            ? _protocol.DefaultExchangeName
            : configuration.ExchangeName;
        _ownsConnectionService = true;
    }
    
    public RpcClientFactory(
        string hostname,
        int port,
        string username,
        string password,
        string? exchangeName = null,
        int retryCount = 3,
        int recoveryInterval = 3,
        IRpcProtocol? protocol = null)
    {
        _protocol = protocol ?? new DefaultJsonProtocol();
        _exchangeName = exchangeName ?? _protocol.DefaultExchangeName;
        
        var config = new RabbitMqConfiguration
        {
            Hostname = hostname,
            Port = port,
            Username = username,
            Password = password,
            ExchangeName = _exchangeName,
            RetryCount = retryCount,
            RecoveryInterval = recoveryInterval
        };
        
        _loggerFactory = NullLoggerFactory.Instance;
        var connectionLogger = _loggerFactory.CreateLogger<RabbitMqConnectionService>();
        _connectionService = new RabbitMqConnectionService(connectionLogger, Options.Create(config));
        
        _ownsConnectionService = true;
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
            _protocol,
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