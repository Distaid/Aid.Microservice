using System.Text.Json;

namespace Aid.Microservice.Shared.Models;

public record RpcRequest
{
    public string Method { get; init; } = string.Empty;
    public Dictionary<string, JsonElement>? Parameters { get; init; }
}