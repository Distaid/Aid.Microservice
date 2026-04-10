namespace Aid.Microservice.Server.Proxy;

/// <summary>
/// Factory for creating RPC proxies to call other services from within a service.
/// </summary>
public interface IRpcProxyFactory
{
    /// <summary>
    /// Creates a proxy to call a remote service.
    /// </summary>
    /// <param name="targetServiceName">The name of the target service (as defined by <c>[Microservice]</c> or its alias).</param>
    /// <returns>An <see cref="IRpcProxy"/> bound to the specified service.</returns>
    IRpcProxy CreateProxy(string targetServiceName);
}