using System.Text.Json;
using Aid.Microservice.Shared.Models;

namespace Aid.Microservice.Shared.Interfaces;

public interface IRpcProtocol
{
    /// <summary>
    /// Exchange type in RabbitMQ (Direct, Topic, Fanout).
    /// </summary>
    string ExchangeType { get; }

    /// <summary>
    /// Exchange name by default (if not specified in appsettings).
    /// </summary>
    string DefaultExchangeName { get; }

    /// <summary>
    /// Default serializer for this protocol.
    /// </summary>
    IRequestSerializer DefaultSerializer { get; }

    /// <summary>
    /// Contents MIME-type (for message properties).
    /// </summary>
    string ContentType => DefaultSerializer.ContentType;

    /// <summary>
    /// Create body and routing key for request.
    /// </summary>
    (byte[] Body, string RoutingKey) CreateRequest(string serviceName, string methodName, object? parameters, JsonSerializerOptions options);

    /// <summary>
    /// Parse response body.
    /// </summary>
    RpcResponse ParseResponse(ReadOnlySpan<byte> body, JsonSerializerOptions options) => DefaultSerializer.ParseResponse(body, options);

    /// <summary>
    /// Returns Routing Key for binding services channel to Exchange.
    /// </summary>
    string GetServiceBindingKey(string serviceName);

    /// <summary>
    /// Parse entering request.
    /// </summary>
    /// <param name="body">Messahe body</param>
    /// <param name="routingKey">Routing key (may contain method name)</param>
    /// <param name="options">JSON options</param>
    RpcRequest ParseRequest(ReadOnlySpan<byte> body, string routingKey, JsonSerializerOptions options) => DefaultSerializer.ParseRequest(body, routingKey, options);

    /// <summary>
    /// Serialize response.
    /// </summary>
    byte[] CreateResponse(RpcResponse response, JsonSerializerOptions options) => DefaultSerializer.CreateResponse(response, options);
}