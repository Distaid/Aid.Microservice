using Aid.Microservice.Shared.Attributes;
using Aid.Microservice.Shared.Protocols;

namespace Aid.Microservice.Server.Example.Services;

/// <summary>
/// Service that communicates with Python Nameko services.
/// All methods use NamekoSerializer, which auto-assigns the "nameko-rpc" exchange.
/// 
/// Format: {"args": [], "kwargs": {...}, "context_data": {...}}
/// </summary>
[Microservice("nameko_service", SerializerType = typeof(NamekoSerializer))]
public class NamekoService
{
    [RpcCallable]
    public int Add(int a, int b) => a + b;
}
