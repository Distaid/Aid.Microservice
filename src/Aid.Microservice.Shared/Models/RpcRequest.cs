using System.Text.Json;

namespace Aid.Microservice.Shared.Models;

public class RpcRequest
{
    public string Method { get; set; } = string.Empty;
    public Dictionary<string, JsonElement>? Parameters { get; set; }
}