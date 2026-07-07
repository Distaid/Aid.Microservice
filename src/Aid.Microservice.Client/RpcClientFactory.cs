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
        _protocol = protocol;
        _exchangeName = config.Value.ExchangeName ?? protocol.DefaultExchangeName;
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
        return CreateClient(serviceName, _protocol, _exchangeName);
    }

    public IRpcClient CreateClient(string serviceName, IRpcProtocol protocol)
    {
        var exchangeName = string.IsNullOrEmpty(_exchangeName) || _exchangeName == _protocol.DefaultExchangeName
            ? protocol.DefaultExchangeName
            : _exchangeName;

        return CreateClient(serviceName, protocol, exchangeName);
    }

    public IRpcClient CreateClient(string serviceName, IRpcProtocol protocol, string exchangeName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException("Service name must be specified", nameof(serviceName));
        }

        var key = $"{serviceName}|{protocol.GetType().Name}|{exchangeName}";
        
        while (true)
        {
            if (_clients.TryGetValue(key, out var existingClient))
            {
                if (existingClient is RefCountedRpcClient refCounted)
                {
                    try
                    {
                        refCounted.Increment();
                        return refCounted;
                    }
                    catch (ObjectDisposedException)
                    {
                        // The client was disposed just as we fetched it. Loop and try again.
                    }
                }
            }

            var newClient = new RpcClient(
                _connectionService,
                _loggerFactory.CreateLogger<RpcClient>(),
                protocol,
                serviceName,
                exchangeName);

            var wrapper = new RefCountedRpcClient(this, key, newClient);

            if (_clients.TryAdd(key, wrapper))
            {
                return wrapper;
            }
            
            // Clean up the concurrently created unused client
            newClient.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private void RemoveFromCache(string key)
    {
        _clients.TryRemove(key, out _);
    }

    private sealed class RefCountedRpcClient(RpcClientFactory owner, string key, IRpcClient inner) : IRpcClient
    {
        private int _refCount = 1;
        private readonly object _lock = new();
        private bool _disposed;

        public void Increment()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(RefCountedRpcClient));
                }
                _refCount++;
            }
        }

        public Task InitializeAsync(CancellationToken token = default) => inner.InitializeAsync(token);

        public Task CallAsync(string method, object? parameters = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            => inner.CallAsync(method, parameters, timeout, cancellationToken);

        public Task<TResponse?> CallAsync<TResponse>(string method, object? parameters = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            => inner.CallAsync<TResponse>(method, parameters, timeout, cancellationToken);

        public async ValueTask DisposeAsync()
        {
            bool shouldDispose = false;
            lock (_lock)
            {
                if (!_disposed)
                {
                    _refCount--;
                    if (_refCount <= 0)
                    {
                        _disposed = true;
                        shouldDispose = true;
                    }
                }
            }

            if (shouldDispose)
            {
                await inner.DisposeAsync();
                owner.RemoveFromCache(key);
            }
        }

        public async Task ForceDisposeAsync()
        {
            bool shouldDispose = false;
            lock (_lock)
            {
                if (!_disposed)
                {
                    _disposed = true;
                    shouldDispose = true;
                }
            }

            if (shouldDispose)
            {
                await inner.DisposeAsync();
                owner.RemoveFromCache(key);
            }
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        var clientsList = _clients.Values.ToList();
        foreach (var client in clientsList)
        {
            try
            {
                if (client is RefCountedRpcClient refCounted)
                {
                    await refCounted.ForceDisposeAsync();
                }
                else
                {
                    await client.DisposeAsync();
                }
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