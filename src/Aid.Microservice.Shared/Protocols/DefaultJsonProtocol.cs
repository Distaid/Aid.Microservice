using System.Text.Json;
using Aid.Microservice.Shared.Interfaces;

namespace Aid.Microservice.Shared.Protocols;

public class DefaultJsonProtocol : IRpcProtocol
{
    public string ExchangeType => RabbitMQ.Client.ExchangeType.Topic;
    public string DefaultExchangeName => "aid_rpc";
    public IRequestSerializer DefaultSerializer { get; } = new DefaultJsonSerializer();

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