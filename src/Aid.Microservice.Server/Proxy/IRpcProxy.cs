namespace Aid.Microservice.Server.Proxy;

/// <summary>
/// A lightweight proxy to call methods on a remote RPC service.
/// Typically created via <see cref="IRpcProxyFactory"/>.
/// </summary>
public interface IRpcProxy
{
    /// <summary>
    /// Calls a remote method without expecting a return value.
    /// </summary>
    /// <param name="method">The method name (as defined by <c>[RpcCallable]</c> or its alias).</param>
    /// <param name="parameters">An anonymous object with named arguments.</param>
    /// <param name="timeout">Call timeout. Defaults to 30 seconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CallAsync(
        string method,
        object? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls a remote method and deserializes the result.
    /// </summary>
    /// <typeparam name="TResponse">The expected return type.</typeparam>
    /// <param name="method">The method name (as defined by <c>[RpcCallable]</c> or its alias).</param>
    /// <param name="parameters">An anonymous object with named arguments.</param>
    /// <param name="timeout">Call timeout. Defaults to 30 seconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized result, or <c>null</c> if the method returns void.</returns>
    Task<TResponse?> CallAsync<TResponse>(
        string method,
        object? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}