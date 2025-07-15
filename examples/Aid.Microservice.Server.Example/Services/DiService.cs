using Aid.Microservice.Shared.Attributes;
using Microsoft.Extensions.Logging;

namespace Aid.Microservice.Server.Example.Services;

[Microservice]
public class DiService(ILogger<DiService> logger)
{
    [RpcCallable]
    public void Log()
    {
        logger.LogInformation("DiService called");
    }
}