using System.Text.Json;
using System.Text.Json.Serialization;
using Aid.Microservice.Shared.Interfaces;
using Aid.Microservice.Shared.Models;
using Aid.Microservice.Shared.Serialization;

namespace Aid.Microservice.Shared.Protocols;

/// <summary>
/// Serializer for the Nameko (Python) protocol.
/// Request format: {"args": [...], "kwargs": {...}, "context_data": {...}}
/// Response format: {"result": ...} or {"error": {"exc_type": ..., "value": ..., "exc_tb": ...}}
/// </summary>
public class NamekoSerializer : IRequestSerializer
{
    public string ContentType => "application/json";
    public string ExchangeName => "nameko-rpc";

    public byte[] CreateRequest(string serviceName, string methodName, object? parameters, JsonSerializerOptions options)
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

        var dto = new NamekoRequestDto
        {
            Args = args,
            Kwargs = kwargs ?? new Dictionary<string, object>(),
            ContextData = new Dictionary<string, object>()
        };

        return JsonSerializer.SerializeToUtf8Bytes(dto, RpcSharedJsonContext.Default.NamekoRequestDto);
    }

    public RpcRequest ParseRequest(ReadOnlySpan<byte> body, string routingKey, JsonSerializerOptions options)
    {
        var dto = JsonSerializer.Deserialize(body, RpcSharedJsonContext.Default.NamekoRequestDto);

        var method = "";
        var parts = routingKey.Split('.');
        if (parts.Length >= 2)
        {
            method = parts.Last();
        }

        Dictionary<string, JsonElement>? parameters = null;
        if (dto?.Kwargs is { Count: > 0 })
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(dto.Kwargs, RpcSharedJsonContext.Default.DictionaryStringObject);
            parameters = JsonSerializer.Deserialize(json, RpcSharedJsonContext.Default.DictionaryStringJsonElement);
        }

        return new RpcRequest
        {
            Method = method,
            Parameters = parameters
        };
    }

    public byte[] CreateResponse(RpcResponse response, JsonSerializerOptions options)
    {
        using var stream = new System.IO.MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();

        if (response.IsSuccess)
        {
            writer.WritePropertyName("result");
            WriteValue(writer, response.Result);
            writer.WriteNull("error");
        }
        else
        {
            writer.WriteNull("result");
            writer.WriteStartObject("error");
            writer.WriteString("exc_type", response.Error!.ErrorType ?? "RpcError");
            writer.WriteString("value", response.Error.Message);
            if (!string.IsNullOrEmpty(response.Error.StackTrace))
            {
                writer.WriteString("exc_tb", response.Error.StackTrace);
            }
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case int i:
                writer.WriteNumberValue(i);
                break;
            case long l:
                writer.WriteNumberValue(l);
                break;
            case double d:
                writer.WriteNumberValue(d);
                break;
            case decimal dec:
                writer.WriteNumberValue(dec);
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case string s:
                writer.WriteStringValue(s);
                break;
            case Guid g:
                writer.WriteStringValue(g);
                break;
            case DateTime dt:
                writer.WriteStringValue(dt);
                break;
            case DateTimeOffset dto:
                writer.WriteStringValue(dto);
                break;
            case DateOnly donly:
                writer.WriteStringValue(donly.ToString("O"));
                break;
            case TimeOnly tonly:
                writer.WriteStringValue(tonly.ToString("O"));
                break;
            case JsonElement je:
                je.WriteTo(writer);
                break;
            default:
                writer.WriteStringValue(value.ToString());
                break;
        }
    }

    public RpcResponse ParseResponse(ReadOnlySpan<byte> body, JsonSerializerOptions options)
    {
        var dto = JsonSerializer.Deserialize(body, RpcSharedJsonContext.Default.NamekoResponseDto);

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
                Error = new RpcError(message, errorType: errorType, stackTrace: stackTrace)
            };
        }

        return new RpcResponse { Result = dto.Result };
    }

    private static object GetValue(JsonElement element)
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

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Dynamic serialization of arbitrary parameter objects")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Dynamic property reflection fallback")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Dynamic serialization of arbitrary parameter objects")]
    private Dictionary<string, object>? ConvertParametersToDictionary(object? parameters, JsonSerializerOptions options)
    {
        if (parameters == null)
        {
            return null;
        }

        if (parameters is Dictionary<string, object> dict)
        {
            return dict;
        }

        try
        {
            using var doc = JsonSerializer.SerializeToDocument(parameters, parameters.GetType(), options);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var result = new Dictionary<string, object>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                result[prop.Name] = GetValue(prop.Value);
            }
            return result;
        }
        catch (InvalidOperationException)
        {
            var result = new Dictionary<string, object>();
            foreach (var prop in parameters.GetType().GetProperties())
            {
                var val = prop.GetValue(parameters);
                if (val != null)
                {
                    result[prop.Name] = val;
                }
            }
            return result;
        }
    }
}

public class NamekoRequestDto
{
    [JsonPropertyName("args")]
    public object[] Args { get; set; } = [];

    [JsonPropertyName("kwargs")]
    public Dictionary<string, object>? Kwargs { get; set; }

    [JsonPropertyName("context_data")]
    public Dictionary<string, object> ContextData { get; set; } = new();
}

public class NamekoResponseDto
{
    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    public Dictionary<string, object>? Error { get; set; }
}
