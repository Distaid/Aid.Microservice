using System.Text.Json;
using Aid.Microservice.Shared.Interfaces;
using Aid.Microservice.Shared.Models;

namespace Aid.Microservice.Shared.Protocols;

public class DefaultJsonProtocol : IRpcProtocol
{
    public string ExchangeType => RabbitMQ.Client.ExchangeType.Topic;
    public string DefaultExchangeName => "aid_rpc";
    public string ContentType => "application/json";
    
    public (byte[] Body, string RoutingKey) CreateRequest(string serviceName, string methodName, object? parameters, JsonSerializerOptions options)
    {
        var request = new RpcRequest
        {
            Method = methodName.ToLowerInvariant(),
            Parameters = ConvertParameters(parameters, options)
        };
        
        var body = JsonSerializer.SerializeToUtf8Bytes(request, options);
        var routingKey = $"{serviceName}.{methodName.ToLowerInvariant()}";
        
        return (body, routingKey);
    }
    
    public RpcResponse ParseResponse(ReadOnlySpan<byte> body, JsonSerializerOptions options)
    {
        var response = JsonSerializer.Deserialize<RpcResponse>(body, options);
        return response ?? new RpcResponse { Error = new RpcError("Empty response body", "ProtocolError") };
    }
    
    public string GetServiceBindingKey(string serviceName)
    {
        return $"{serviceName}.*";
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