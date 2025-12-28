using Aid.Microservice.Server.Proxy;
using Aid.Microservice.Shared.Attributes;

namespace Aid.Microservice.Server.Example.Services;

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