using System.Text.Json;
using Aid.Microservice.Server.Infrastructure;
using Aid.Microservice.Shared.Configuration;
using Aid.Microservice.Shared.Interfaces;
using Aid.Microservice.Shared.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Aid.Microservice.Server.Hosting;

public class RpcListenerHost(
    ILogger<RpcListenerHost> logger,
    IHostApplicationLifetime hostApplicationLifetime,
    IRabbitMqConnectionService connectionService,
    IRpcEndpointRegistry registry,
    IRpcRequestDispatcher dispatcher,
    ISerializerRegistry serializerRegistry,
    IOptions<RabbitMqConfiguration> rabbitConfig,
    IRpcProtocol protocol)
    : BackgroundService
{
    private readonly RabbitMqConfiguration _rabbitConfig = rabbitConfig.Value;
    private readonly List<IChannel> _activeChannels = [];
    private readonly HashSet<string> _declaredExchanges = new(StringComparer.OrdinalIgnoreCase);

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var endpoints = registry.GetRegisteredServiceEndpoints().ToList();
        if (endpoints.Count == 0)
        {
            logger.LogWarning("No RPC endpoints registered. Listener will not start");
            hostApplicationLifetime.StopApplication();
            return;
        }

        var globalOverride = !string.IsNullOrWhiteSpace(_rabbitConfig.ExchangeName);

        var uniqueExchanges = globalOverride
            ? endpoints.Select(_ => _rabbitConfig.ExchangeName!).Distinct().ToList()
            : endpoints.Select(e => e.ExchangeName).Distinct().ToList();

        logger.LogInformation(
            "Starting RPC Listeners for {Count} service endpoints across {Exchanges} exchanges",
            endpoints.Count, uniqueExchanges.Count);

        if (!await connectionService.TryConnectAsync(stoppingToken))
        {
            logger.LogCritical("Could not connect to RabbitMQ. Shutting down host.");
            hostApplicationLifetime.StopApplication();
            return;
        }

        var exchangeDeclared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var exchange in uniqueExchanges)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                var channel = await connectionService.CreateChannelAsync(stoppingToken);
                _activeChannels.Add(channel);

                await channel.ExchangeDeclareAsync(
                    exchange: exchange,
                    type: protocol.ExchangeType,
                    durable: true,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: stoppingToken);

                exchangeDeclared.Add(exchange);
                _declaredExchanges.Add(exchange);
                
                logger.LogInformation("Exchange '{Exchange}' declared (Type: {Type})", exchange, protocol.ExchangeType);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to declare exchange '{Exchange}'", exchange);
            }
        }

        foreach (var (serviceName, exchangeName) in endpoints)
        {
            if (stoppingToken.IsCancellationRequested) break;

            var actualExchange = globalOverride ? _rabbitConfig.ExchangeName! : exchangeName;
            if (!exchangeDeclared.Contains(actualExchange))
            {
                logger.LogWarning("Exchange '{Exchange}' was not declared. Skipping service '{Service}'", actualExchange, serviceName);
                continue;
            }

            try
            {
                await StartServiceListenerAsync(serviceName, actualExchange, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start listener for service '{Service}' on exchange '{Exchange}'", serviceName, actualExchange);
            }
        }
        logger.LogInformation("Server start working");
    }

    private async Task StartServiceListenerAsync(
        string serviceName,
        string exchangeName,
        CancellationToken token)
    {
        var channel = await connectionService.CreateChannelAsync(token);
        _activeChannels.Add(channel);
        var queueName = exchangeName.EndsWith('_') || exchangeName.EndsWith('.')
            ? $"{exchangeName}{serviceName}"
            : $"{exchangeName}_{serviceName}";

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: true,
            arguments: null,
            cancellationToken: token);

        var bindingKey = protocol.GetServiceBindingKey(serviceName);
        await channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: bindingKey,
            arguments: null,
            cancellationToken: token);

        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: token);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                await ProcessMessageAsync(serviceName, exchangeName, channel, ea);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "CRITICAL: Unhandled exception in consumer loop for {Service}", serviceName);
            }
        };

        await channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer, cancellationToken: token);
        logger.LogInformation("Service '{Service}' listening on Queue '{Queue}' (Exchange: {Exchange})", serviceName, queueName, exchangeName);
    }

    private async Task ProcessMessageAsync(string serviceName, string exchangeName, IChannel channel, BasicDeliverEventArgs ea)
    {
        var props = ea.BasicProperties;
        var replyTo = props.ReplyTo;
        var correlationId = props.CorrelationId;

        if (string.IsNullOrEmpty(replyTo) || string.IsNullOrEmpty(correlationId))
        {
            logger.LogWarning("Received RPC message without ReplyTo/CorrelationId. Ignoring. Tag: {Tag}", ea.DeliveryTag);
            await channel.BasicNackAsync(ea.DeliveryTag, false, false);
            return;
        }

        var serializer = ResolveSerializer(serviceName, ea.RoutingKey);

        RpcResponse response;
        try
        {
            var request = serializer.ParseRequest(ea.Body.Span, ea.RoutingKey, _jsonOptions);

            if (string.IsNullOrWhiteSpace(request.Method))
            {
                response = new RpcResponse
                {
                    Error = new RpcError("Invalid Request Format", errorType: "ProtocolError")
                };
            }
            else
            {
                response = await dispatcher.DispatchAsync(serviceName, request.Method, request.Parameters);
            }
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "JSON Deserialization failed for {Service}", serviceName);
            response = new RpcResponse { Error = new RpcError("Invalid JSON", errorType: "JsonException") };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error processing message for {Service}", serviceName);
            response = new RpcResponse { Error = new RpcError("Internal Server Error") };
        }

        try
        {
            var responseBytes = serializer.CreateResponse(response, _jsonOptions);

            var replyProps = new BasicProperties
            {
                CorrelationId = correlationId,
                ContentType = serializer.ContentType
            };

            await channel.BasicPublishAsync(
                exchange: exchangeName,
                routingKey: replyTo,
                mandatory: true,
                basicProperties: replyProps,
                body: responseBytes);

            await channel.BasicAckAsync(ea.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send response back to {ReplyTo}", replyTo);
            try
            {
                await channel.BasicNackAsync(ea.DeliveryTag, false, false);
            }
            catch
            {
                // ignored
            }
        }
    }

    private IRequestSerializer ResolveSerializer(string serviceName, string routingKey)
    {
        var parts = routingKey.Split('.');
        var methodName = parts.Length >= 2 ? parts.Last() : null;

        if (!string.IsNullOrEmpty(methodName) &&
            registry.TryGetMethod(serviceName, methodName, out var methodInfo) &&
            methodInfo?.SerializerType != null)
        {
            var customSerializer = serializerRegistry.GetSerializer(methodInfo.SerializerType);
            if (customSerializer != null)
            {
                return customSerializer;
            }
        }

        return protocol.DefaultSerializer;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping RPC Listeners...");

        if (_rabbitConfig.DeleteExchangesOnShutdown && _declaredExchanges.Count > 0)
        {
            logger.LogInformation("Deleting {Count} declared exchanges...", _declaredExchanges.Count);

            try
            {
                var channel = await connectionService.CreateChannelAsync(cancellationToken);
                foreach (var exchange in _declaredExchanges)
                {
                    try
                    {
                        await channel.ExchangeDeleteAsync(exchange, cancellationToken: cancellationToken);
                        logger.LogInformation("Exchange '{Exchange}' deleted", exchange);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to delete exchange '{Exchange}'", exchange);
                    }
                }

                await channel.CloseAsync(cancellationToken);
                channel.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to create channel for exchange cleanup");
            }
        }

        foreach (var channel in _activeChannels)
        {
            try
            {
                if (channel.IsOpen)
                {
                    await channel.CloseAsync(cancellationToken);
                    channel.Dispose();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("Error closing channel: {Message}", ex.Message);
            }
        }

        _activeChannels.Clear();
        _declaredExchanges.Clear();
        await base.StopAsync(cancellationToken);
    }
}