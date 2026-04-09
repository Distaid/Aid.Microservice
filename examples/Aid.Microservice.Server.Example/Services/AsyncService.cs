using Aid.Microservice.Shared.Attributes;

namespace Aid.Microservice.Server.Example.Services;

/// <summary>
/// Demonstrates async method support.
/// Async RPC methods are awaited and their results are returned to the caller.
/// </summary>
[Microservice]
public class AsyncService
{
    [RpcCallable]
    public async Task Delay(int seconds)
    {
        await Task.Delay(TimeSpan.FromSeconds(seconds));
    }
}