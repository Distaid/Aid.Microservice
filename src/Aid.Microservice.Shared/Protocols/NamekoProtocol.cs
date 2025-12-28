using System.Text.Json;
using System.Text.Json.Serialization;
using Aid.Microservice.Shared.Interfaces;
using Aid.Microservice.Shared.Models;

namespace Aid.Microservice.Shared.Protocols;

public class NamekoProtocol : IRpcProtocol
{
    public string ExchangeType => RabbitMQ.Client.ExchangeType.Topic;
    public string DefaultExchangeName => "nameko-rpc";
    public string ContentType => "application/json";

    public (byte[] Body, string RoutingKey) CreateRequest(string serviceName, string methodName, object? parameters, JsonSerializerOptions options)
    {
        object[] args;
        Dictionary<string, object>? kwargs;
        
        if (parameters is RpcNamekoRequest namekoRequest)
        {
            args = namekoRequest.Args;
            kwargs = ConvertParametersToDictionary(namekoRequest.Kwargs, options);
        }
        else
        {
            args = [];
            kwargs = ConvertParametersToDictionary(parameters, options);
        }

        var requestDto = new NamekoRequestDto
        {
            Args = args,
            Kwargs = kwargs ?? new Dictionary<string, object>(),
            ContextData = new Dictionary<string, object>()
        };

        var body = JsonSerializer.SerializeToUtf8Bytes(requestDto, options);
        
        var routingKey = $"{serviceName}.{methodName.ToLowerInvariant()}";

        return (body, routingKey);
    }

    public RpcResponse ParseResponse(ReadOnlySpan<byte> body, JsonSerializerOptions options)
    {
        var dto = JsonSerializer.Deserialize<NamekoResponseDto>(body, options);

        if (dto == null)
        {
            return new RpcResponse { Error = new RpcError("Empty response from Nameko", "ProtocolError") };
        }

        if (dto.Error != null)
        {
            var errorType = dto.Error.GetValueOrDefault("exc_type")?.ToString() ?? "NamekoError";
            var message = dto.Error.GetValueOrDefault("value")?.ToString() ?? "Unknown error";
            var stackTrace = dto.Error.GetValueOrDefault("exc_tb")?.ToString();

            return new RpcResponse 
            { 
                Error = new RpcError(message, stackTrace, errorType) 
            };
        }

        return new RpcResponse { Result = dto.Result };
    }

    public string GetServiceBindingKey(string serviceName)
    {
        return $"{serviceName}.*";
    }

    public RpcRequest ParseRequest(ReadOnlySpan<byte> body, string routingKey, JsonSerializerOptions options)
    {
        var dto = JsonSerializer.Deserialize<NamekoRequestDto>(body, options);
        
        var method = "";
        var parts = routingKey.Split('.');
        if (parts.Length >= 2)
        {
            method = parts.Last();
        }

        
        Dictionary<string, JsonElement>? parameters = null;
        if (dto?.Kwargs is { Count: > 0 })
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(dto.Kwargs, options);
            parameters = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, options);
        }

        return new RpcRequest
        {
            Method = method,
            Parameters = parameters
        };
    }

    public byte[] CreateResponse(RpcResponse response, JsonSerializerOptions options)
    {
        var dto = new NamekoResponseDto();

        if (response.IsSuccess)
        {
            dto.Result = response.Result;
            dto.Error = null;
        }
        else
        {
            dto.Result = null;
            dto.Error = new Dictionary<string, object>
            {
                { "exc_type", response.Error!.ErrorType ?? "RpcError" },
                { "value", response.Error.Message }
            };

            if (!string.IsNullOrEmpty(response.Error.StackTrace))
            {
                dto.Error["exc_tb"] = response.Error.StackTrace;
            }
        }

        return JsonSerializer.SerializeToUtf8Bytes(dto, options);
    }
    
    private Dictionary<string, object>? ConvertParametersToDictionary(object? parameters, JsonSerializerOptions options)
    {
        if (parameters == null)
        {
            return null;
        }
        
        using var doc = JsonSerializer.SerializeToDocument(parameters, options);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var dict = new Dictionary<string, object>();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            dict[prop.Name] = GetValue(prop.Value);
        }
        
        return dict;
    }
    
    private object GetValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString()!,
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            _ => element
        };
    }
}

internal class NamekoRequestDto
{
    [JsonPropertyName("args")]
    public object[] Args { get; set; } = Array.Empty<object>();

    [JsonPropertyName("kwargs")]
    public Dictionary<string, object>? Kwargs { get; set; }

    [JsonPropertyName("context_data")]
    public Dictionary<string, object> ContextData { get; set; } = new();
}

internal class NamekoResponseDto
{
    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    public Dictionary<string, object>? Error { get; set; }
}