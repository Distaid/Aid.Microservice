using Aid.Microservice.Client;

namespace Aid.Microservice.Server.Proxy;

public class RpcProxy : IRpcProxy
{
    private readonly string _targetServiceName;
    private readonly RpcClient _rpcClient;

    public RpcProxy(string targetServiceName, RpcClient rpcClient)
    {
        if (string.IsNullOrWhiteSpace(targetServiceName))
        {
            throw new ArgumentNullException(nameof(targetServiceName));
        }

        _targetServiceName = targetServiceName.ToLowerInvariant();
        _rpcClient = rpcClient ?? throw new ArgumentNullException(nameof(rpcClient));
    }
    
    public Task<TResponse?> CallAsync<TResponse>(
        string method,
        object? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        return _rpcClient.CallAsync<TResponse>(
            _targetServiceName,
            method,
            parameters,
            timeout,
            cancellationToken);
    }
    
    public Task CallAsync(
        string method,
        object? parameters = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        return _rpcClient.CallAsync<int>(
            _targetServiceName,
            method,
            parameters,
            timeout,
            cancellationToken);
    }
}