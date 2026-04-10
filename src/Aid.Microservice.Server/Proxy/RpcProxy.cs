using Aid.Microservice.Client.Infrastructure;

namespace Aid.Microservice.Server.Proxy;

/// <summary>
/// Implementation of <see cref="IRpcProxy"/> that delegates RPC calls to an <see cref="IRpcClient"/>.
/// Used by <see cref="IRpcProxyFactory"/> to create proxies for inter-service communication.
/// </summary>
public class RpcProxy(IRpcClient client) : IRpcProxy
{
    /// <inheritdoc />
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

    /// <inheritdoc />
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