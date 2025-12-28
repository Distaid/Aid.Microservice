using System.Collections.Concurrent;
using System.Text.Json;
using Aid.Microservice.Client.Infrastructure;
using Aid.Microservice.Client.Models;
using Aid.Microservice.Shared.Interfaces;
using Aid.Microservice.Shared.Models;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Aid.Microservice.Client;

public class RpcClient(
    IRabbitMqConnectionService connectionService,
    ILogger<RpcClient> logger,
    IRpcProtocol protocol,
    string targetServiceName,
    string exchangeName,
    bool ownsConnectionService = false)
    : IRpcClient
{
    private readonly string _exchangeName = !string.IsNullOrWhiteSpace(exchangeName) 
        ? exchangeName 
        : protocol.DefaultExchangeName;
    
    private IChannel? _publishChannel;
    private readonly SemaphoreSlim _publishLock = new(1, 1);
    
    private IChannel? _subscribeChannel;
    private string? _replyQueueName;
    private AsyncEventingBasicConsumer? _consumer;
    
    private readonly ConcurrentDictionary<string, TaskCompletionSource<byte[]>> _callbackMapper = new();
    private bool _isInitialized;
    
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null
    };

    public async Task InitializeAsync(CancellationToken token = default)
    {
        if (_isInitialized)
        {
            return;
        }

        if (!await connectionService.TryConnectAsync(token))
        {
            throw new InvalidOperationException("RPC Client failed to connect to RabbitMQ.");
        }

        _publishChannel = await connectionService.CreateChannelAsync(token);
        _subscribeChannel = await connectionService.CreateChannelAsync(token);

        var queueResult = await _subscribeChannel.QueueDeclareAsync(
            queue: "", 
            durable: false, 
            exclusive: true, 
            autoDelete: true, 
            arguments: null,
            cancellationToken: token);
            
        _replyQueueName = queueResult.QueueName;

        _consumer = new AsyncEventingBasicConsumer(_subscribeChannel);
        _consumer.ReceivedAsync += OnResponseReceivedAsync;

        await _subscribeChannel.BasicConsumeAsync(queue: _replyQueueName, autoAck: true, consumer: _consumer, cancellationToken: token);

        _isInitialized = true;
        logger.LogInformation("RPC Client initialized. Bound to service: {Service}. Reply Queue: {Queue}", targetServiceName, _replyQueueName);
    }
    
    private async Task OnResponseReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        var correlationId = ea.BasicProperties.CorrelationId;
        if (!string.IsNullOrEmpty(correlationId) && _callbackMapper.TryRemove(correlationId, out var tcs))
        {
            var body = ea.Body.ToArray();
            tcs.TrySetResult(body);
        }
        await Task.CompletedTask;
    }
    
    public Task CallAsync(string method, object? parameters = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        EnsureTargetServiceBound();
        return CallAsync<object>(method, parameters, timeout, cancellationToken);
    }
    
    private void EnsureTargetServiceBound()
    {
        if (string.IsNullOrEmpty(targetServiceName))
        {
            throw new InvalidOperationException(
                "This RpcClient is not bound to a specific service. " +
                "Use the overload CallAsync(serviceName, methodName, ...) or create the client via CreateClient(\"serviceName\")");
        }
    }
    
    public async Task<TResponse?> CallAsync<TResponse>(
        string method,
        object? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
        {
            await InitializeAsync(cancellationToken);
        }

        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(30);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(effectiveTimeout);

        var correlationId = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_callbackMapper.TryAdd(correlationId, tcs)) throw new InvalidOperationException("CorrelationId collision");

        try
        {
            var (body, routingKey) = protocol.CreateRequest(targetServiceName, method, parameters, _jsonOptions);
            
            var props = new BasicProperties
            {
                CorrelationId = correlationId,
                ReplyTo = _replyQueueName,
                ContentType = protocol.ContentType
            };

            await _publishLock.WaitAsync(cts.Token);
            try
            {
                await _publishChannel!.BasicPublishAsync(
                    exchange: _exchangeName,
                    routingKey: routingKey,
                    mandatory: true,
                    basicProperties: props,
                    body: body,
                    cancellationToken: cts.Token);
            }
            finally
            {
                _publishLock.Release();
            }

            await using (cts.Token.Register(() => { if (_callbackMapper.TryRemove(correlationId, out var r)) r.TrySetCanceled(); }))
            {
                var responseBytes = await tcs.Task.ConfigureAwait(false);
                return HandleResponse<TResponse>(responseBytes, correlationId);
            }
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"RPC call to {targetServiceName}.{method} timed out.");
        }
        catch
        {
            _callbackMapper.TryRemove(correlationId, out _);
            throw;
        }
    }
    
    private TResponse? HandleResponse<TResponse>(byte[] responseBytes, string correlationId)
    {
        var rpcResponse = protocol.ParseResponse(responseBytes, _jsonOptions);
        
        if (rpcResponse == null)
        {
            throw new RpcCallException("Empty response", correlationId);
        }

        if (!rpcResponse.IsSuccess)
        {
            throw new RpcCallException(rpcResponse.Error!, correlationId);
        }

        if (rpcResponse.Result is JsonElement je)
        {
            return je.Deserialize<TResponse>(_jsonOptions);
        }

        if (rpcResponse.Result is TResponse tr)
        {
            return tr;
        }
        
        var json = JsonSerializer.Serialize(rpcResponse.Result, _jsonOptions);
        return JsonSerializer.Deserialize<TResponse>(json, _jsonOptions);
    }
    
    public async ValueTask DisposeAsync()
    {
        foreach (var tcs in _callbackMapper.Values)
        {
            tcs.TrySetCanceled();
        }
        _callbackMapper.Clear();
        _publishLock.Dispose();

        if (_publishChannel != null)
        {
            await _publishChannel.DisposeAsync();
        }

        if (_subscribeChannel != null)
        {
            await _subscribeChannel.DisposeAsync();
        }

        if (ownsConnectionService)
        {
            await connectionService.DisposeAsync();
        }
        
        GC.SuppressFinalize(this);
    }
}