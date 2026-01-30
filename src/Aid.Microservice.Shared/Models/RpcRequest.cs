using System.Text.Json;
using System.Text.Json.Serialization;
using Aid.Microservice.Shared.Converters;

namespace Aid.Microservice.Shared.Models;

public record RpcRequest
{
    public string Method { get; init; } = string.Empty;
    [JsonConverter(typeof(CaseInsensitiveDictionaryConverter))]
    public Dictionary<string, JsonElement>? Parameters { get; init; }
}