using Aid.Microservice.Shared.Attributes;

namespace Aid.Microservice.Server.Example.Services;

[MicroserviceQuery("clear_cache")]
public class ClearCacheQueryHandler
{
    // Named Handle instead of HandleAsync, which should trigger a warning in logs
    public async Task Handle(CancellationToken token)
    {
        await Task.Delay(10, token);
    }
}
