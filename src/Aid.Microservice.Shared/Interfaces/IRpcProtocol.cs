using System.Text.Json;
using Aid.Microservice.Shared.Models;

namespace Aid.Microservice.Shared.Interfaces;

public interface IRpcProtocol
{
    /// <summary>
    /// Exchange type в RabbitMQ (Direct, Topic, Fanout).
    /// </summary>
    string ExchangeType { get; }

    /// <summary>
    /// Exchange name by default (if not specified in appsettings).
    /// </summary>
    string DefaultExchangeName { get; }

    /// <summary>
    /// Contents MIME-type (for message properties).
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// Create body and routing key for request.
    /// </summary>
    (byte[] Body, string RoutingKey) CreateRequest(string serviceName, string methodName, object? parameters, JsonSerializerOptions options);

    /// <summary>
    /// Parse request body.
    /// </summary>
    RpcResponse ParseResponse(ReadOnlySpan<byte> body, JsonSerializerOptions options);

    // --- Серверная сторона ---

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
    RpcRequest ParseRequest(ReadOnlySpan<byte> body, string routingKey, JsonSerializerOptions options);

    /// <summary>
    /// Serialize response.
    /// </summary>
    byte[] CreateResponse(RpcResponse response, JsonSerializerOptions options);
}