namespace Aid.Microservice.Client.Infrastructure;

/// <summary>
/// A client for making RPC calls to a remote service over RabbitMQ.
/// </summary>
public interface IRpcClient : IAsyncDisposable
{
    /// <summary>
    /// Initializes the client by creating a publish channel and reply queue.
    /// Called automatically on the first <see cref="CallAsync"/> invocation.
    /// </summary>
    Task InitializeAsync(CancellationToken token = default);

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

    /// <summary>
    /// Calls a remote query without expecting a return value.
    /// Automatically prefixes the query name with "query.".
    /// </summary>
    Task CallQuery(
        string queryName,
        object? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls a remote query and deserializes the result.
    /// Automatically prefixes the query name with "query.".
    /// </summary>
    Task<TResponse?> CallQuery<TResponse>(
        string queryName,
        object? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls a remote query without expecting a return value.
    /// Automatically prefixes the query name with "query.".
    /// </summary>
    Task CallQueryAsync(
        string queryName,
        object? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls a remote query and deserializes the result.
    /// Automatically prefixes the query name with "query.".
    /// </summary>
    Task<TResponse?> CallQueryAsync<TResponse>(
        string queryName,
        object? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}