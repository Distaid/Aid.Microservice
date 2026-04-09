using System.Text.Json;
using Aid.Microservice.Shared.Interfaces;

namespace Aid.Microservice.Shared.Protocols;

/// <summary>
/// Protocol for interoperability with Nameko (Python).
/// Uses Topic exchange with service name as routing key base, matching Nameko conventions.
/// Message body contains Nameko format: {"args": [...], "kwargs": {...}, "context_data": {...}}
/// </summary>
public class NamekoProtocol : IRpcProtocol
{
    public string ExchangeType => RabbitMQ.Client.ExchangeType.Topic;
    public string DefaultExchangeName => "nameko-rpc";
    public IRequestSerializer DefaultSerializer { get; } = new NamekoSerializer();

    public (byte[] Body, string RoutingKey) CreateRequest(string serviceName, string methodName, object? parameters, JsonSerializerOptions options)
    {
        var body = DefaultSerializer.CreateRequest(serviceName, methodName, parameters, options);
        var routingKey = $"{serviceName}.{methodName.ToLowerInvariant()}";
        return (body, routingKey);
    }

    public string GetServiceBindingKey(string serviceName)
    {
        return $"{serviceName}.*";
    }
}