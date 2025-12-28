using Aid.Microservice.Client.Infrastructure;

namespace Aid.Microservice.Server.Proxy;

public class RpcProxyFactory(IRpcClientFactory clientFactory) : IRpcProxyFactory
{
    public IRpcProxy CreateProxy(string targetServiceName)
    {
        var client = clientFactory.CreateClient(targetServiceName);
        return new RpcProxy(client);
    }
}