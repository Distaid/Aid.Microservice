namespace Aid.Microservice.Server.Proxy;

public interface IRpcProxy
{
    Task<TResponse?> CallAsync<TResponse>(
        string method,
        object? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    Task CallAsync(
        string method,
        object? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}