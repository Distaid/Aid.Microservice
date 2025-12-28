using Aid.Microservice.Client.Infrastructure;

namespace Aid.Microservice.Server.Proxy;

public class RpcProxy(IRpcClient client) : IRpcProxy
{
    public Task CallAsync(
        string method,
        object? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        return client.CallAsync(
            method,
            parameters,
            timeout,
            cancellationToken);
    }
    
    public Task<TResponse?> CallAsync<TResponse>(
        string method,
        object? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        return client.CallAsync<TResponse>(
            method,
            parameters,
            timeout,
            cancellationToken);
    }
}