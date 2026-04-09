using Aid.Microservice.Shared.Attributes;
using Aid.Microservice.Shared.Protocols;

namespace Aid.Microservice.Server.Example.Services;

/// <summary>
/// Mixed service: different methods listen on different exchanges.
/// 
/// - nameko_add → NamekoSerializer → "nameko-rpc" exchange
/// - default_add → DefaultJsonSerializer → "aid_rpc" exchange
/// 
/// The server automatically detects all unique exchanges used by the service
/// and creates a queue on each one.
/// </summary>
[Microservice("mixed_service")]
public class MixedService
{
    /// <summary>
    /// This method uses NamekoSerializer, so it listens on "nameko-rpc" exchange.
    /// Can be called by Python Nameko services.
    /// </summary>
    [RpcCallable("nameko_add", SerializerType = typeof(NamekoSerializer))]
    public int NamekoAdd(int a, int b) => a + b;

    /// <summary>
    /// This method uses the default serializer, so it listens on "aid_rpc" exchange.
    /// Standard .NET-to-.NET communication.
    /// </summary>
    [RpcCallable("default_add")]
    public int DefaultAdd(int a, int b) => a + b;
}
