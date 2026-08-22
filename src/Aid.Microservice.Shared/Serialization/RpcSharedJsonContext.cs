using System.Text.Json;
using System.Text.Json.Serialization;
using Aid.Microservice.Shared.Models;
using Aid.Microservice.Shared.Protocols;

namespace Aid.Microservice.Shared.Serialization;

/// <summary>
/// Source-generated JsonSerializerContext for core RPC models and protocols.
/// Ensures full NativeAOT compatibility for message envelope processing.
/// </summary>
[JsonSerializable(typeof(RpcRequest))]
[JsonSerializable(typeof(RpcResponse))]
[JsonSerializable(typeof(RpcError))]
[JsonSerializable(typeof(NamekoRequestDto))]
[JsonSerializable(typeof(NamekoResponseDto))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(double))]
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never
)]
public partial class RpcSharedJsonContext : JsonSerializerContext
{
}
