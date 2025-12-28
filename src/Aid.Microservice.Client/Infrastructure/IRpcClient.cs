namespace Aid.Microservice.Client.Infrastructure;

public interface IRpcClient : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken token = default);
    
    Task CallAsync(
        string method,
        object? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    Task<TResponse?> CallAsync<TResponse>(
        string method,
        object? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}