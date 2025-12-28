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
    IRabbitMqConnectionService connectionService,
    IRpcEndpointRegistry registry,
    IRpcRequestDispatcher dispatcher,
    IOptions<RabbitMqConfiguration> rabbitConfig,
    IRpcProtocol protocol)
    : BackgroundService
{
    private readonly RabbitMqConfiguration _rabbitConfig = rabbitConfig.Value;
    private readonly List<IChannel> _activeChannels = new();
    
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var services = registry.GetRegisteredServices().ToList();
        if (services.Count == 0)
        {
            logger.LogWarning("No RPC endpoints registered. Listener will not start");
            return;
        }
        
        var exchangeName = !string.IsNullOrWhiteSpace(_rabbitConfig.ExchangeName) 
            ? _rabbitConfig.ExchangeName 
            : protocol.DefaultExchangeName;

        logger.LogInformation("Starting RPC Listeners for {Count} services. Exchange: {Ex} (Type: {Type})", services.Count, exchangeName, protocol.ExchangeType);
        if (!await connectionService.TryConnectAsync(stoppingToken))
        {
            logger.LogCritical("Could not connect to RabbitMQ. RPC Server stopping.");
        }
        
        foreach (var serviceName in services)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            
            try
            {
                await StartServiceListenerAsync(serviceName, exchangeName, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start listener for service '{Service}'", serviceName);
            }
        }
    }

    private async Task StartServiceListenerAsync(string serviceName, string exchangeName, CancellationToken token)
    {
        var channel = await connectionService.CreateChannelAsync(token);
        _activeChannels.Add(channel);
        
        var queueName = exchangeName.EndsWith('_') || exchangeName.EndsWith('.') 
            ? $"{exchangeName}{serviceName}" 
            : $"{exchangeName}_{serviceName}";
        
        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: protocol.ExchangeType, 
            durable: true,
            autoDelete: false, 
            arguments: null, 
            cancellationToken: token);
        
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
                await ProcessMessageAsync(serviceName, channel, ea);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "CRITICAL: Unhandled exception in consumer loop for {Service}", serviceName);
            }
        };
        
        await channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer, cancellationToken: token);
        logger.LogInformation("Service '{Service}' listening on Queue '{Queue}'", serviceName, queueName);
    }

    private async Task ProcessMessageAsync(string serviceName, IChannel channel, BasicDeliverEventArgs ea)
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
        
        RpcResponse response;
        try
        {
            var request = protocol.ParseRequest(ea.Body.Span, ea.RoutingKey, _jsonOptions);
            
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
            var responseBytes = protocol.CreateResponse(response, _jsonOptions);
            
            var replyProps = new BasicProperties
            {
                CorrelationId = correlationId
            };

            await channel.BasicPublishAsync(
                exchange: "", 
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

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping RPC Listeners...");
        
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
        await base.StopAsync(cancellationToken);
    }
}