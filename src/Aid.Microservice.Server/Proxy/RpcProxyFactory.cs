using Aid.Microservice.Client.Infrastructure;

namespace Aid.Microservice.Server.Proxy;

/// <summary>
/// Implementation of <see cref="IRpcProxyFactory"/> that creates proxies backed by <see cref="IRpcClient"/> instances.
/// Uses the default protocol and exchange configured for the server.
/// </summary>
public class RpcProxyFactory(IRpcClientFactory clientFactory) : IRpcProxyFactory
{
    /// <inheritdoc />
    public IRpcProxy CreateProxy(string targetServiceName)
    {
        var client = clientFactory.CreateClient(targetServiceName);
        return new RpcProxy(client);
    }
}