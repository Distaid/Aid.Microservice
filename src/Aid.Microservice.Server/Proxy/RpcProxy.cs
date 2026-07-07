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

    /// <inheritdoc />
    public Task CallQuery(
        string queryName,
        object? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        return client.CallQuery(
            queryName,
            parameters,
            timeout,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResponse?> CallQuery<TResponse>(
        string queryName,
        object? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        return client.CallQuery<TResponse>(
            queryName,
            parameters,
            timeout,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task CallQueryAsync(
        string queryName,
        object? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        return client.CallQueryAsync(
            queryName,
            parameters,
            timeout,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResponse?> CallQueryAsync<TResponse>(
        string queryName,
        object? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        return client.CallQueryAsync<TResponse>(
            queryName,
            parameters,
            timeout,
            cancellationToken);
    }
}