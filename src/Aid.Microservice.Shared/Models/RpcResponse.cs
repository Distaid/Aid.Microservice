using System.Text.Json.Serialization;

namespace Aid.Microservice.Shared.Models;

public record RpcResponse
{
    public object? Result { get; init; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RpcError? Error { get; set; }

    [JsonIgnore]
    public bool IsSuccess => Error == null;
}