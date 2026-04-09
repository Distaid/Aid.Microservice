using Aid.Microservice.Shared.Attributes;
using Microsoft.Extensions.Logging;

namespace Aid.Microservice.Server.Example.Services;

/// <summary>
/// Demonstrates dependency injection support.
/// Services are registered as Scoped and can inject any registered dependencies,
/// such as ILogger, DbContext, configuration, etc.
/// </summary>
[Microservice]
public class DiService(ILogger<DiService> logger)
{
    [RpcCallable]
    public void Log()
    {
        logger.LogInformation("DiService called");
    }
}