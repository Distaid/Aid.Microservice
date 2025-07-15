using Aid.Microservice.Server.Configuration;
using Aid.Microservice.Server.Infrastructure;
using Aid.Microservice.Shared.Attributes;
using Aid.Microservice.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Aid.Microservice.Server.Hosting;

public class RpcServerHost : IHostedService, IAsyncDisposable
{
    private readonly ILogger<RpcServerHost> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IRabbitMqConnectionService _connectionService;
    private readonly List<RpcListenerInfo> _listeners = [];
    private readonly HostConfiguration _hostConfiguration;

    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, RpcMethodInfo>> RpcEndpoints = new();

    private static bool _endpointsDiscovered;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null
    };

    public RpcServerHost(
        ILogger<RpcServerHost> logger,
        IServiceProvider serviceProvider,
        IRabbitMqConnectionService connectionService,
        IOptions<HostConfiguration> hostConfiguration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _connectionService = connectionService;
        _hostConfiguration = hostConfiguration.Value;

        EnsureEndpointsDiscovered();
    }

    private void EnsureEndpointsDiscovered()
    {
        if (_endpointsDiscovered)
        {
            return;
        }

        if (_hostConfiguration.ShowServiceRegisterMetrics)
        {
            _logger.LogInformation("Discovering RPC endpoints...");
        }

        var serviceTypes = Assembly
            .GetEntryAssembly()!
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.GetCustomAttribute<MicroserviceAttribute>() is not null);

        var serviceCount = 0;
        var methodCount = 0;
        foreach (var serviceType in serviceTypes)
        {
            var serviceAttr = serviceType.GetCustomAttribute<MicroserviceAttribute>();
            if (serviceAttr == null)
            {
                continue;
            }

            serviceAttr.SetServiceName(serviceType);
            var serviceName = serviceAttr.ServiceName;

            var methodMap = RpcEndpoints.GetOrAdd(serviceName, _ => new ConcurrentDictionary<string, RpcMethodInfo>());
            var currentServiceMethodCount = 0;

            var methods = serviceType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttribute<RpcCallableAttribute>() is not null);

            foreach (var method in methods)
            {
                var callableAttr = method.GetCustomAttribute<RpcCallableAttribute>();
                if (callableAttr == null)
                {
                    continue;
                }

                callableAttr.SetMethodName(method);
                var methodName = callableAttr.MethodName;

                if (!methodMap.TryAdd(methodName, new RpcMethodInfo(serviceType, method)))
                {
                    _logger.LogWarning(
                        "Duplicate RPC alias '{Alias}' for service '{Service}'. Method {DeclaringType}.{MethodName} ignored",
                        methodName, serviceName, serviceType.Name, method.Name);
                }
                else
                {
                    _logger.LogDebug(
                        "RPC Endpoint registered: Service='{Service}', Alias='{Alias}', Method='{MethodName}'",
                        serviceName, methodName, method.Name);
                    currentServiceMethodCount++;
                }
            }

            if (currentServiceMethodCount > 0)
            {
                if (_hostConfiguration.ShowServiceRegisterMetrics)
                {
                    _logger.LogInformation("Service '{Service}' registered with {Count} RPC methods", serviceName, currentServiceMethodCount);
                }
                serviceCount++;
                methodCount += currentServiceMethodCount;
            }
            else
            {
                RpcEndpoints.TryRemove(serviceName, out _);
                _logger.LogWarning("Service '{Service}' has no [RpcCallable] methods, skipping listener setup", serviceName);
            }
        }

        _endpointsDiscovered = true;
        if (_hostConfiguration.ShowServiceRegisterMetrics)
        {
            _logger.LogInformation(
                "RPC endpoint discovery complete. Found {ServiceCount} services with {MethodCount} total RPC methods",
                serviceCount, methodCount);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("RPC Server Host is starting...");
        if (!_endpointsDiscovered || RpcEndpoints.IsEmpty)
        {
            _logger.LogWarning("No RPC endpoints discovered. Host will not start listeners");
            return;
        }

        if (!await _connectionService.TryConnectAsync(cancellationToken))
        {
            _logger.LogCritical("RabbitMQ connection failed after retries. RPC Server cannot start listeners");
            throw new InvalidOperationException("Failed to connect to RabbitMQ for RPC Server");
        }

        foreach (var (serviceName, serviceMethods) in RpcEndpoints)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (serviceMethods.IsEmpty)
            {
                continue;
            }

            try
            {
                var serviceType = serviceMethods.First().Value.ServiceType;
                var serviceAttr = serviceType.GetCustomAttribute<MicroserviceAttribute>();
                if (serviceAttr == null)
                {
                    throw new InvalidOperationException($"Attribute not found for {serviceType.Name}");
                }
                serviceAttr.SetServiceName(serviceType);

                var queueName = serviceAttr.QueueName;
                var channel = await _connectionService.CreateChannelAsync(cancellationToken);

                // _logger.LogInformation("Declaring queue '{QueueName}' for service '{Service}'...", queueName, serviceName);
                await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: cancellationToken);
                await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: cancellationToken);

                var consumer = new AsyncEventingBasicConsumer(channel);

                var currentServiceName = serviceName;
                consumer.ReceivedAsync += async (_, ea) =>
                {
                    try
                    {
                        await HandleRpcRequest(currentServiceName, channel, ea);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogCritical(ex,
                            "Unhandled exception in RPC message handler loop for service '{Service}'. Consumer might be affected",
                            currentServiceName);
                    }
                };

                var consumerTag = await channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer, cancellationToken: cancellationToken);
                _listeners.Add(new RpcListenerInfo(serviceName, queueName, channel, consumerTag));
                _logger.LogInformation(
                    "RPC Listener started for service '{Service}' on queue '{QueueName}' [Tag: {ConsumerTag}]",
                    serviceName, queueName, consumerTag);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex,
                    "Failed to start RPC listener for service '{Service}'. This service will not process requests",
                    serviceName);
            }
        }

        await Task.CompletedTask;
    }

    private async Task HandleRpcRequest(string serviceName, IChannel channel, BasicDeliverEventArgs ea)
    {
        var rpcResponse = new RpcResponse();
        var correlationId = ea.BasicProperties.CorrelationId;
        var replyTo = ea.BasicProperties.ReplyTo;
        var responseProps = new BasicProperties
        {
            CorrelationId = correlationId,
            ReplyTo = replyTo
        };

        if (string.IsNullOrEmpty(replyTo) || string.IsNullOrEmpty(correlationId))
        {
            _logger.LogWarning(
                "RPC message ignored for service '{Service}': Missing 'ReplyTo' ('{ReplyQueue}') or 'CorrelationId' ('{CorrId}'). DeliveryTag: {DeliveryTag}.",
                serviceName, replyTo ?? "null", correlationId ?? "null", ea.DeliveryTag);
            try
            {
                await channel.BasicNackAsync(ea.DeliveryTag, false, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to Nack ignored message.");
            }

            return;
        }

        try
        {
            var body = ea.Body.ToArray();
            var requestJson = Encoding.UTF8.GetString(body);

            var rpcRequest = JsonSerializer.Deserialize<RpcRequest>(requestJson, _jsonOptions);
            if (rpcRequest == null || string.IsNullOrWhiteSpace(rpcRequest.Method))
            {
                throw new ArgumentException("Invalid RPC request format or missing MethodAlias");
            }

            var method = rpcRequest.Method.ToLowerInvariant();
            _logger.LogInformation(
                "--> RPC Request received. service='{Service}' parameters={Body} correlation_id={CorrelationId} reply_to={ReplyTo}",
                serviceName, requestJson, correlationId, replyTo);

            if (!RpcEndpoints.TryGetValue(serviceName, out var serviceMethods) || !serviceMethods.TryGetValue(method, out var methodInfo))
            {
                throw new MissingMethodException($"Method '{method}' not found for service '{serviceName}'");
            }

            using var scope = _serviceProvider.CreateScope();
            var serviceInstance = scope.ServiceProvider.GetService(methodInfo.ServiceType);
            if (serviceInstance == null)
            {
                throw new InvalidOperationException($"Could not resolve service '{methodInfo.ServiceType.Name}'. Is it registered in DI?");
            }

            var methodArgs = PrepareMethodArguments(methodInfo.Method, rpcRequest.Parameters);

            var result = methodInfo.Method.Invoke(serviceInstance, methodArgs);

            if (result is Task taskResult)
            {
                await taskResult.ConfigureAwait(false);
                var returnType = methodInfo.Method.ReturnType;
                if (returnType.IsGenericType && taskResult.Exception == null)
                {
                    try
                    {
                        rpcResponse.Result = ((dynamic)taskResult).Result;
                    }
                    catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException rbEx)
                    {
                        _logger.LogWarning(rbEx, "Could not get Result from Task<T> for {Service}.{Method}", serviceName, method);
                        rpcResponse.Result = null;
                    }
                    catch (Exception taskEx)
                    {
                        throw new TargetInvocationException($"Error accessing Task Result for {serviceName}.{method}", taskEx);
                    }
                }
                else if (taskResult.Exception != null)
                {
                    throw new TargetInvocationException($"Async method {serviceName}.{method} failed", taskResult.Exception.InnerException ?? taskResult.Exception);
                }
                else
                {
                    rpcResponse.Result = null;
                }
            }
            else
            {
                rpcResponse.Result = methodInfo.Method.ReturnType == typeof(void) ? null : result;
            }

            rpcResponse.Error = null;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            _logger.LogError(ex.InnerException,
                "Error executing RPC method {Service}.{MethodAlias}. CorrelationId: {CorrelationId}",
                serviceName, GetMethodAliasFromRequest(ea.Body) ?? "unknown", correlationId);
            rpcResponse.Result = null;
            rpcResponse.Error = new RpcError(ex.InnerException, _logger.IsEnabled(LogLevel.Debug));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing RPC request for {Service}.{MethodAlias}. CorrelationId: {CorrelationId}",
                serviceName, GetMethodAliasFromRequest(ea.Body) ?? "unknown", correlationId);
            rpcResponse.Result = null;
            rpcResponse.Error = new RpcError(ex, _logger.IsEnabled(LogLevel.Debug));
        }
        finally
        {
            try
            {
                var responseJson = JsonSerializer.Serialize(rpcResponse, _jsonOptions);
                var responseBytes = Encoding.UTF8.GetBytes(responseJson);

                await channel.BasicPublishAsync(exchange: "", routingKey: replyTo, basicProperties: responseProps, body: responseBytes, mandatory: true);
                await channel.BasicAckAsync(ea.DeliveryTag, false);
                _logger.LogDebug("Message Ack'd. DeliveryTag: {DeliveryTag}", ea.DeliveryTag);
            }
            catch (Exception pubEx)
            {
                _logger.LogCritical(pubEx,
                    "CRITICAL: Failed to publish RPC response or ACK original message! CorrelationId: {CorrelationId}. DeliveryTag: {DeliveryTag}. Message might be lost or redelivered",
                    correlationId, ea.DeliveryTag);
                try
                {
                    await channel.BasicNackAsync(ea.DeliveryTag, false, false);
                }
                catch (Exception nackEx)
                {
                    _logger.LogError(nackEx, "Failed to NACK message after publish/ack failure. DeliveryTag: {DeliveryTag}", ea.DeliveryTag);
                }
            }
        }
    }

    private object?[]? PrepareMethodArguments(MethodInfo method, Dictionary<string, JsonElement>? parameters)
    {
        if (parameters is null)
        {
            return null;
        }

        var methodParams = method.GetParameters();
        var args = new object?[methodParams.Length];

        for (var i = 0; i < methodParams.Length; i++)
        {
            var paramInfo = methodParams[i];
            var paramName = paramInfo.Name;
            if (string.IsNullOrEmpty(paramName))
            {
                throw new InvalidOperationException($"Method {method.Name} has parameter without name at index {i}");
            }

            var (key, jsonElement) = parameters.FirstOrDefault(kvp => kvp.Key.Equals(paramName, StringComparison.OrdinalIgnoreCase));
            var keyFound = key is not null;

            if (keyFound)
            {
                try
                {
                    args[i] = jsonElement.ValueKind == JsonValueKind.Null
                        ? null
                        : jsonElement.Deserialize(paramInfo.ParameterType, _jsonOptions);
                }
                catch (JsonException jsonEx)
                {
                    throw new ArgumentException(
                        $"Invalid JSON format for parameter '{paramName}'. Expected type: {paramInfo.ParameterType.Name}. Error: {jsonEx.Message}",
                        jsonEx);
                }
                catch (NotSupportedException nse)
                {
                    throw new ArgumentException(
                        $"Type '{paramInfo.ParameterType.Name}' for parameter '{paramName}' is not supported by JsonSerializer",
                        nse);
                }
            }
            else if (paramInfo.HasDefaultValue)
            {
                args[i] = paramInfo.DefaultValue;
            }
            else if (paramInfo.ParameterType.IsValueType && Nullable.GetUnderlyingType(paramInfo.ParameterType) is null)
            {
                throw new ArgumentException($"Missing required non-nullable parameter '{paramName}' for method '{method.Name}'");
            }
            else
            {
                args[i] = null;
            }
        }

        return args;
    }

    private string? GetMethodAliasFromRequest(ReadOnlyMemory<byte> body)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(body);
            if (jsonDoc.RootElement.TryGetProperty("Method", out var prop))
            {
                return prop.GetString();
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("RPC Server Host is stopping...");
        foreach (var (serviceName, queueName, channel, consumerTag) in _listeners)
        {
            try
            {
                if (!channel.IsOpen)
                {
                    continue;
                }
                _logger.LogInformation(
                    "Stopping RPC listener for service '{Service}' (Tag: {ConsumerTag}) on queue '{QueueName}'...",
                    serviceName, consumerTag, queueName);

                await channel.BasicCancelAsync(consumerTag, cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is AlreadyClosedException or IOException)
            {
                _logger.LogWarning(
                    "Channel for service '{Service}' was already closed or encountered IO error during stop: {ErrorMessage}",
                    serviceName, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping RPC listener consumer for service '{Service}'", serviceName);
            }
        }

        _listeners.Clear();
        _logger.LogInformation("All RPC listener consumer tags cancelled.");
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var listener in _listeners)
        {
            try
            {
                listener.Channel.Dispose();
            }
            catch
            {
                // ignore
            }
        }

        _listeners.Clear();
        GC.SuppressFinalize(this);
        await Task.CompletedTask;
    }

    private record RpcListenerInfo(string ServiceName, string QueueName, IChannel Channel, string ConsumerTag);

    private record RpcMethodInfo(Type ServiceType, MethodInfo Method);
}