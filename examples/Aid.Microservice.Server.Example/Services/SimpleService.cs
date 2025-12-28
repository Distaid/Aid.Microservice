using Aid.Microservice.Shared.Attributes;

namespace Aid.Microservice.Server.Example.Services;

[Microservice]
public class SimpleService
{
    [RpcCallable]
    public int Multiple(int a, int b) => a * b;
}