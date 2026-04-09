using Aid.Microservice.Shared.Attributes;

namespace Aid.Microservice.Server.Example.Services;

/// <summary>
/// Basic service with default settings.
/// Uses DefaultJsonSerializer on the "aid_rpc" exchange.
/// No explicit configuration needed — everything is inferred.
/// </summary>
[Microservice]
public class SimpleService
{
    [RpcCallable]
    public int Multiple(int a, int b) => a * b;
}
