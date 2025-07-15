namespace Aid.Microservice.Server.Proxy;

public interface IRpcProxyFactory
{
    IRpcProxy CreateProxy(string targetServiceName);
}