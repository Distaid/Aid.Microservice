using System.Text.Json;
using Aid.Microservice.Shared.Interfaces;
using Aid.Microservice.Shared.Models;
using Aid.Microservice.Shared.Serialization;

namespace Aid.Microservice.Shared.Protocols;

/// <summary>
/// Serializer for the default Aid.Microservice JSON protocol.
/// Format: {"Method": "...", "Parameters": {...}}
/// </summary>
public class DefaultJsonSerializer : IRequestSerializer
{
    public string ContentType => "application/json";
    public string ExchangeName => "aid_rpc";

    public byte[] CreateRequest(string serviceName, string methodName, object? parameters, JsonSerializerOptions options)
    {
        var request = new RpcRequest
        {
            Method = methodName.ToLowerInvariant(),
            Parameters = ConvertParameters(parameters, options)
        };
        return JsonSerializer.SerializeToUtf8Bytes(request, RpcSharedJsonContext.Default.RpcRequest);
    }

    public RpcRequest ParseRequest(ReadOnlySpan<byte> body, string routingKey, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize(body, RpcSharedJsonContext.Default.RpcRequest)
               ?? new RpcRequest();
    }

    public byte[] CreateResponse(RpcResponse response, JsonSerializerOptions options)
    {
        using var stream = new System.IO.MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();

        if (response.Error != null)
        {
            writer.WriteNull("result");
            writer.WritePropertyName("error");
            JsonSerializer.Serialize(writer, response.Error, RpcSharedJsonContext.Default.RpcError);
        }
        else
        {
            writer.WritePropertyName("result");
            WriteValue(writer, response.Result);
            writer.WriteNull("error");
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
        var response = JsonSerializer.Deserialize(body, RpcSharedJsonContext.Default.RpcResponse);
        return response ?? new RpcResponse { Error = new RpcError("Empty response body", "ProtocolError") };
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Dynamic serialization of arbitrary parameter objects")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Dynamic property reflection fallback")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Dynamic serialization of arbitrary parameter objects")]
    private Dictionary<string, JsonElement>? ConvertParameters(object? parameters, JsonSerializerOptions options)
    {
        if (parameters == null)
        {
            return null;
        }

        if (parameters is Dictionary<string, JsonElement> dict)
        {
            return dict;
        }

        if (parameters is JsonElement { ValueKind: JsonValueKind.Object } je)
        {
            return je.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var doc = JsonSerializer.SerializeToDocument(parameters, parameters.GetType(), options);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                ? doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.OrdinalIgnoreCase)
                : null;
        }
        catch (InvalidOperationException)
        {
            // NativeAOT fallback when reflection-based serialization is disabled
            var fallbackDict = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in parameters.GetType().GetProperties())
            {
                var val = prop.GetValue(parameters);
                var elem = val switch
                {
                    null => JsonDocument.Parse("null").RootElement.Clone(),
                    int i => JsonDocument.Parse(i.ToString(System.Globalization.CultureInfo.InvariantCulture)).RootElement.Clone(),
                    long l => JsonDocument.Parse(l.ToString(System.Globalization.CultureInfo.InvariantCulture)).RootElement.Clone(),
                    double d => JsonDocument.Parse(d.ToString(System.Globalization.CultureInfo.InvariantCulture)).RootElement.Clone(),
                    decimal dec => JsonDocument.Parse(dec.ToString(System.Globalization.CultureInfo.InvariantCulture)).RootElement.Clone(),
                    bool b => JsonDocument.Parse(b ? "true" : "false").RootElement.Clone(),
                    string s => JsonDocument.Parse(JsonSerializer.Serialize(s, RpcSharedJsonContext.Default.String)).RootElement.Clone(),
                    JsonElement jeVal => jeVal.Clone(),
                    _ => JsonDocument.Parse(JsonSerializer.Serialize(val.ToString(), RpcSharedJsonContext.Default.String)).RootElement.Clone()
                };
                fallbackDict[prop.Name] = elem;
            }
            return fallbackDict;
        }
    }
}
