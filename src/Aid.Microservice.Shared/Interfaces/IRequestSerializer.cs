using System.Text.Json;
using Aid.Microservice.Shared.Models;

namespace Aid.Microservice.Shared.Interfaces;

/// <summary>
/// Handles serialization and deserialization of RPC message bodies.
/// Independent of RabbitMQ infrastructure (exchange, queue, binding).
/// </summary>
public interface IRequestSerializer
{
    /// <summary>
    /// MIME-type of the message content (for message properties).
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// Suggested exchange name for this serializer.
    /// Used when the service does not explicitly declare its own exchanges.
    /// Returns null if the serializer is exchange-agnostic.
    /// </summary>
    string? ExchangeName { get; }

    /// <summary>
    /// Creates the request body from service name, method name, and parameters.
    /// </summary>
    /// <param name="serviceName">Name of the target service.</param>
    /// <param name="methodName">Name of the target method.</param>
    /// <param name="parameters">Parameters object (can be an anonymous type, RpcNamekoRequest, etc.).</param>
    /// <param name="options">JSON serializer options.</param>
    byte[] CreateRequest(string serviceName, string methodName, object? parameters, JsonSerializerOptions options);

    /// <summary>
    /// Parses an incoming request from the message body.
    /// </summary>
    /// <param name="body">Message body.</param>
    /// <param name="routingKey">Routing key (may contain the method name).</param>
    /// <param name="options">JSON serializer options.</param>
    RpcRequest ParseRequest(ReadOnlySpan<byte> body, string routingKey, JsonSerializerOptions options);

    /// <summary>
    /// Creates the response body.
    /// </summary>
    byte[] CreateResponse(RpcResponse response, JsonSerializerOptions options);

    /// <summary>
    /// Parses a response body (used on the client side).
    /// </summary>
    RpcResponse ParseResponse(ReadOnlySpan<byte> body, JsonSerializerOptions options);
}
