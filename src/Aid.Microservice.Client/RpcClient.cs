using System.Collections.Concurrent;
using System.Text.Json;
using Aid.Microservice.Client.Infrastructure;
using Aid.Microservice.Client.Models;
using Aid.Microservice.Shared;
using Aid.Microservice.Shared.Models;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Aid.Microservice.Client;

public class RpcClient : IRpcClient
{
    private readonly IRabbitMqConnectionService _connectionService;
    private readonly ILogger _logger;
    private readonly bool _ownsConnectionService;
    
    private readonly string _targetServiceName;
    private readonly string _exchangeName;
    
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
    
    public RpcClient(
        IRabbitMqConnectionService connectionService,
        ILogger<RpcClient> logger,
        string targetServiceName,
        string exchangeName,
        bool ownsConnectionService = false)
    {
        _connectionService = connectionService;
        _logger = logger;
        _targetServiceName = targetServiceName;
        _exchangeName = exchangeName;
        _ownsConnectionService = ownsConnectionService;
    }

    public async Task InitializeAsync(CancellationToken token = default)
    {
        if (_isInitialized)
        {
            return;
        }

        if (!await _connectionService.TryConnectAsync(token))
        {
            throw new InvalidOperationException("RPC Client failed to connect to RabbitMQ.");
        }

        _publishChannel = await _connectionService.CreateChannelAsync(token);
        _subscribeChannel = await _connectionService.CreateChannelAsync(token);

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
        _logger.LogInformation("RPC Client initialized. Bound to service: {Service}. Reply Queue: {Queue}", 
            _targetServiceName ?? "<ANY>", _replyQueueName);
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
        if (string.IsNullOrEmpty(_targetServiceName))
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
            var request = new RpcRequest
            {
                Method = method.ToLowerInvariant(),
                Parameters = ConvertParameters(parameters)
            };
            var body = JsonSerializer.SerializeToUtf8Bytes(request, _jsonOptions);
            
            var props = new BasicProperties { CorrelationId = correlationId, ReplyTo = _replyQueueName };

            await _publishLock.WaitAsync(cts.Token);
            try
            {
                await _publishChannel!.BasicPublishAsync(exchange: _exchangeName, routingKey: _targetServiceName, mandatory: true, basicProperties: props, body: body, cancellationToken: cts.Token);
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
            throw new TimeoutException($"RPC call to {_targetServiceName}.{method} timed out.");
        }
        catch
        {
            _callbackMapper.TryRemove(correlationId, out _);
            throw;
        }
    }
    
    private TResponse? HandleResponse<TResponse>(byte[] responseBytes, string correlationId)
    {
        var response = JsonSerializer.Deserialize<RpcResponse>(responseBytes, _jsonOptions);
        if (response == null)
        {
            throw new RpcCallException("Empty response", correlationId);
        }

        if (!response.IsSuccess)
        {
            throw new RpcCallException(response.Error!, correlationId);
        }

        if (response.Result is JsonElement je)
        {
            return je.Deserialize<TResponse>(_jsonOptions);
        }

        if (response.Result is TResponse tr)
        {
            return tr;
        }
        
        var json = JsonSerializer.Serialize(response.Result, _jsonOptions);
        return JsonSerializer.Deserialize<TResponse>(json, _jsonOptions);
    }
    
    private Dictionary<string, JsonElement>? ConvertParameters(object? parameters)
    {
        if (parameters == null)
        {
            return null;
        }
        
        using var doc = JsonSerializer.SerializeToDocument(parameters, _jsonOptions);
        return doc.RootElement.ValueKind == JsonValueKind.Object
            ? doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.OrdinalIgnoreCase)
            : null;
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

        if (_ownsConnectionService)
        {
            await _connectionService.DisposeAsync();
        }
        
        GC.SuppressFinalize(this);
    }
}