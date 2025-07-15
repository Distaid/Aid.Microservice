using Aid.Microservice.Server.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Net.Sockets;

namespace Aid.Microservice.Server.Infrastructure;

public class RabbitMqConnectionService : IRabbitMqConnectionService
{
    private readonly ConnectionFactory _connectionFactory;
    private readonly ILogger<RabbitMqConnectionService> _logger;
    private IConnection? _connection;
    private bool _disposed;
    private readonly int _retryCount;
    private readonly TimeSpan _recoveryInterval;

    public RabbitMqConnectionService(
        ILogger<RabbitMqConnectionService> logger,
        IOptions<RabbitMqConfiguration> rabbitMqConfigOption)
    {
        var rabbitMqConfig = rabbitMqConfigOption.Value;

        _logger = logger;
        _retryCount = rabbitMqConfig.RetryCount;
        _recoveryInterval = TimeSpan.FromSeconds(rabbitMqConfig.RecoveryInterval);

        _connectionFactory = new ConnectionFactory
        {
            HostName = rabbitMqConfig.Hostname,
            Port = rabbitMqConfig.Port,
            UserName = rabbitMqConfig.Username,
            Password = rabbitMqConfig.Password,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = _recoveryInterval
        };

        _logger.LogInformation("RabbitMQ Connection Service configured for {Host}:{Port}", rabbitMqConfig.Hostname, rabbitMqConfig.Port);
    }

    public bool IsConnected => _connection is not null && _connection.IsOpen && !_disposed;

    public async Task<bool> TryConnectAsync(CancellationToken cancellationToken)
    {
        if (IsConnected)
        {
            return true;
        }

        if (_disposed)
        {
            _logger.LogWarning("RabbitMQ connection service is disposed. Cannot connect");
            return false;
        }

        _logger.LogInformation("Attempting to connect to RabbitMQ...");
        for (var retry = 1; retry <= _retryCount; retry++)
        {
            try
            {
                await DisposeConnection();
                _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

                if (IsConnected)
                {
                    _connection.ConnectionShutdownAsync += OnConnectionShutdownAsync;
                    _connection.CallbackExceptionAsync += OnCallbackExceptionAsync;
                    _connection.ConnectionBlockedAsync += OnConnectionBlockedAsync;
                    _connection.ConnectionUnblockedAsync += OnConnectionUnblockedAsync;
                    _logger.LogInformation("RabbitMQ connection established successfully. Client: {ClientName}", _connection!.ClientProvidedName);
                    return true;
                }
                else
                {
                    _logger.LogWarning("CreateConnection succeeded but connection is not open. Attempt {Retry}/{TotalRetries}", retry, _retryCount);
                }
            }
            catch (BrokerUnreachableException ex)
            {
                _logger.LogWarning(ex, "Could not reach RabbitMQ broker. Attempt {Retry}/{TotalRetries}", retry, _retryCount);
            }
            catch (SocketException ex)
            {
                _logger.LogWarning(ex, "Network error connecting to RabbitMQ. Attempt {Retry}/{TotalRetries}", retry, _retryCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to RabbitMQ. Attempt {Retry}/{TotalRetries}", retry, _retryCount);

                if (ex is AuthenticationFailureException) break;
            }

            if (retry < _retryCount)
            {
                await Task.Delay(_recoveryInterval, cancellationToken);
            }
        }

        _logger.LogError("Failed to connect to RabbitMQ after {TotalRetries} attempts", _retryCount);
        return false;
    }

    private async Task DisposeConnection()
    {
        if (_connection is not null)
        {
            try
            {
                _connection.ConnectionShutdownAsync -= OnConnectionShutdownAsync;
                _connection.CallbackExceptionAsync -= OnCallbackExceptionAsync;
                _connection.ConnectionBlockedAsync -= OnConnectionBlockedAsync;
                _connection.ConnectionUnblockedAsync -= OnConnectionUnblockedAsync;

                if (_connection.IsOpen)
                {
                    await _connection.CloseAsync(TimeSpan.FromSeconds(5));
                }
            }
            catch (AlreadyClosedException)
            {
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error during connection close");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during connection close");
            }
            finally
            {
                try
                {
                    _connection.Dispose();
                }
                catch
                {
                    // ignored
                }

                _connection = null;
            }
        }
    }

    private async Task OnConnectionBlockedAsync(object? sender, ConnectionBlockedEventArgs e)
    {
        _logger.LogWarning("RabbitMQ connection BLOCKED. Reason: {Reason}", e.Reason);
        await Task.CompletedTask;
    }

    private async Task OnConnectionUnblockedAsync(object? sender, AsyncEventArgs e)
    {
        _logger.LogInformation("RabbitMQ connection UNBLOCKED");
        await Task.CompletedTask;
    }

    private async Task OnCallbackExceptionAsync(object? sender, CallbackExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "RabbitMQ callback exception occurred. Detail: {@Detail}", e.Detail);
        await Task.CompletedTask;
    }

    private async Task OnConnectionShutdownAsync(object? sender, ShutdownEventArgs reason)
    {
        _logger.LogWarning("RabbitMQ connection SHUTDOWN. Initiator: {Initiator}. Reason: {Reason}", reason.Initiator, reason.ReplyText);
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _logger.LogInformation("Disposing RabbitMQ Connection Service...");
        await DisposeConnection();
        GC.SuppressFinalize(this);
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }

    public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken)
    {
        if (IsConnected)
        {
            return await _connection!.CreateChannelAsync(cancellationToken: cancellationToken);
        }

        _logger.LogWarning("RabbitMQ connection is not open. Attempting to reconnect before creating channel...");
        if (!await TryConnectAsync(cancellationToken))
        {
            throw new InvalidOperationException("RabbitMQ connection unavailable. Cannot create channel");
        }

        return await _connection!.CreateChannelAsync(cancellationToken: cancellationToken);
    }
}