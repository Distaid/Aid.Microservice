using Aid.Microservice.Server.Proxy;
using Aid.Microservice.Shared.Attributes;

namespace Aid.Microservice.Server.Example.Services;

/// <summary>
/// Demonstrates interservice communication via RpcProxyFactory.
/// A service can call another service on the same server using IRpcProxy,
/// enabling composition and delegation patterns.
/// </summary>
[Microservice]
public class ProxyService(IRpcProxyFactory factory)
{
    private readonly IRpcProxy _multipleProxy = factory.CreateProxy("simple");

    [RpcCallable]
    public async Task<string> MultiplyString()
    {
        var result = await _multipleProxy.CallAsync<int>("multiple", new { a = 5, b = 6 });
        return $"5 * 6 = {result}";
    }
}