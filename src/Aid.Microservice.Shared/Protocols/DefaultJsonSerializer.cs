using System.Text.Json;
using Aid.Microservice.Shared.Interfaces;
using Aid.Microservice.Shared.Models;

namespace Aid.Microservice.Shared.Protocols;

/// <summary>
/// Serializer for the default Aid.Microservice JSON protocol.
/// Format: {"Method": "...", "Parameters": {...}}
/// </summary>
public class DefaultJsonSerializer : IRequestSerializer
{
    public string ContentType => "application/json";
    public string? ExchangeName => "aid_rpc";

    public byte[] CreateRequest(string serviceName, string methodName, object? parameters, JsonSerializerOptions options)
    {
        var request = new RpcRequest
        {
            Method = methodName.ToLowerInvariant(),
            Parameters = ConvertParameters(parameters, options)
        };
        return JsonSerializer.SerializeToUtf8Bytes(request, options);
    }

    public RpcRequest ParseRequest(ReadOnlySpan<byte> body, string routingKey, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<RpcRequest>(body, options)
               ?? new RpcRequest();
    }

    public byte[] CreateResponse(RpcResponse response, JsonSerializerOptions options)
    {
        return JsonSerializer.SerializeToUtf8Bytes(response, options);
    }

    public RpcResponse ParseResponse(ReadOnlySpan<byte> body, JsonSerializerOptions options)
    {
        var response = JsonSerializer.Deserialize<RpcResponse>(body, options);
        return response ?? new RpcResponse { Error = new RpcError("Empty response body", "ProtocolError") };
    }

    private Dictionary<string, JsonElement>? ConvertParameters(object? parameters, JsonSerializerOptions options)
    {
        if (parameters == null)
        {
            return null;
        }

        using var doc = JsonSerializer.SerializeToDocument(parameters, options);
        return doc.RootElement.ValueKind == JsonValueKind.Object
            ? doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.OrdinalIgnoreCase)
            : null;
    }
}
