using Aid.Microservice.Shared.Attributes;

namespace Aid.Microservice.Server.Example.Services;

[Microservice]
public class AsyncService
{
    [RpcCallable]
    public async Task Delay(int seconds)
    {
        await Task.Delay(TimeSpan.FromSeconds(seconds));
    }
}