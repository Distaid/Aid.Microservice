using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Aid.Microservice.Client.Configuration;
using Aid.Microservice.Client.Models;
using Aid.Microservice.Shared.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Aid.Microservice.Client;

public class RpcClient : IAsyncDisposable
{
    private readonly ConnectionFactory _connectionFactory;
    private IChannel _channel = null!;
    private IConnection _connection = null!;
    private string _replyQueueName = null!;
    private AsyncEventingBasicConsumer _consumer = null!;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _callbackMapper = new();
    
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null
    };
    
    public RpcClient(RabbitMqConfiguration configuration)
        : this(configuration.Hostname, configuration.Port, configuration.Username, configuration.Password)
    { }
    
    public RpcClient(string hostName, int port, string userName, string password)
    {
        _connectionFactory = new ConnectionFactory
        {
            HostName = hostName,
            UserName = userName,
            Password = password,
            Port = port
        };
    }
    
    public async Task InitializeAsync()
    {
        try
        {
            _connection = await _connectionFactory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();
            
            _replyQueueName = (await _channel.QueueDeclareAsync(queue: "", durable: false, exclusive: true, autoDelete: true)).QueueName;
            _consumer = new AsyncEventingBasicConsumer(_channel);

            _consumer.ReceivedAsync += OnResponseReceivedAsync;

            await _channel.BasicConsumeAsync(consumer: _consumer, queue: _replyQueueName, autoAck: true);
        }
        catch (Exception)
        {
            _channel.Dispose();
            _connection.Dispose();
            throw;
        }
    }
    
    private async Task OnResponseReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        var correlationId = ea.BasicProperties.CorrelationId;
        if (!string.IsNullOrEmpty(correlationId) && _callbackMapper.TryRemove(correlationId, out var tcs))
        {
            var body = ea.Body.ToArray();
            var responseJson = Encoding.UTF8.GetString(body);
            tcs.TrySetResult(responseJson);
        }

        await Task.CompletedTask;
    }
    
    public async Task CallAsynс(
        string service,
        string method,
        object? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        await CallAsync<int>(service, method, parameters, timeout, cancellationToken);
    }
    
    public async Task<TResponse?> CallAsync<TResponse>(
        string service,
        string method,
        object? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout.HasValue)
        {
            cts.CancelAfter(timeout.Value);
        }

        var correlationId = Guid.NewGuid().ToString("N");
        var props = new BasicProperties
        {
            CorrelationId = correlationId,
            ReplyTo = _replyQueueName
        };

        var request = new RpcRequest
        {
            Method = method.ToLowerInvariant(),
            Parameters = ConvertParametersToObjectDictionary(parameters)
        };

        var messageBytes = JsonSerializer.SerializeToUtf8Bytes(request, _jsonOptions);
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_callbackMapper.TryAdd(correlationId, tcs))
        {
            cts.Dispose();
            throw new InvalidOperationException("Failed to register callback due to duplicate CorrelationId.");
        }

        var targetQueue = $"rpc_aid_{service.ToLowerInvariant()}";

        try
        {
            await _channel.BasicPublishAsync(exchange: "", routingKey: targetQueue, basicProperties: props, body: messageBytes, mandatory: true, cancellationToken: cts.Token);

            await using var registration = cts.Token.Register(() =>
            {
                if (!_callbackMapper.TryRemove(correlationId, out var timedOutTcs))
                {
                    return;
                }

                timedOutTcs.TrySetCanceled(cts.Token);
            });

            var responseJson = await tcs.Task.ConfigureAwait(false);

            var rpcResponse = JsonSerializer.Deserialize<RpcResponse>(responseJson, _jsonOptions);

            if (rpcResponse == null)
            {
                throw new RpcCallException("Received null or invalid response from server", correlationId);
            }

            if (rpcResponse.Error != null)
            {
                throw new RpcCallException(rpcResponse.Error, correlationId);
            }

            return rpcResponse.Result switch
            {
                JsonElement { ValueKind: JsonValueKind.Null } when typeof(TResponse).IsClass => default,
                JsonElement resultElement => resultElement.Deserialize<TResponse>(_jsonOptions),
                null => default,
                _ => throw new RpcCallException($"Unexpected result type '{rpcResponse.Result.GetType().Name}' in response", correlationId)
            };
        }
        catch (TaskCanceledException ex)
        {
            throw new TimeoutException(
                $"RPC call timed out or was cancelled after {timeout?.TotalSeconds ?? 0} seconds. CorrelationId: {correlationId}",
                ex);
        }
        catch (Exception)
        {
            _callbackMapper.TryRemove(correlationId, out _);
            throw;
        }
        finally
        {
            cts.Dispose();
        }
    }
    
    private Dictionary<string, JsonElement>? ConvertParametersToObjectDictionary(object? parameters)
    {
        if (parameters == null)
        {
            return null;
        }

        try
        {
            using var jsonDoc = JsonSerializer.SerializeToDocument(parameters, _jsonOptions);

            if (jsonDoc.RootElement.ValueKind == JsonValueKind.Object)
            {
                return jsonDoc.RootElement
                    .EnumerateObject()
                    .ToDictionary(p => p.Name, p => p.Value.Clone(),  StringComparer.OrdinalIgnoreCase);
            }

            return null;
        }
        catch (Exception ex)
        {
            throw new ArgumentException("Failed to convert parameters object for RPC call", ex);
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        foreach (var tcs in _callbackMapper.Values)
        {
            tcs.TrySetCanceled();
        }

        _callbackMapper.Clear();

        await _channel.CloseAsync();
        await _channel.DisposeAsync();
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}