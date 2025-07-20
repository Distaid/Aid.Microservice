using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aid.Microservice.Server.Hosting;

public interface IMicroserviceHostBuilder
{
    /// <summary>
    /// Adds additional services to the container. This can be called multiple times and the results will be additive.
    /// </summary>
    /// <param name="configureDelegate">The delegate for configuring the <see cref="T:Microsoft.Extensions.DependencyInjection.IServiceCollection" /> that will be used to construct the <see cref="T:System.IServiceProvider" /></param>
    /// <returns>The same instance of the <see cref="Aid.Microservice.Server.Hosting.IMicroserviceHostBuilder" /> for chaining</returns>
    IMicroserviceHostBuilder ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureDelegate);
    
    /// <summary>
    /// Runs the given actions to initialize the host. This can only be called once.
    /// </summary>
    /// <returns>An initialized <see cref="Aid.Microservice.Server.Hosting.IMicroserviceHostBuilder" /></returns>
    /// <exception cref="InvalidOperationException">Throws if method called more then one time</exception>
    IMicroserviceHostBuilder Build();
    
    /// <summary>
    /// Runs an application and blocks the calling thread until host shutdown is triggered and all <see cref="Aid.Microservice.Server.Hosting.IMicroserviceHostBuilder" /> instances are stopped.
    /// </summary>
    void Run();
    
    /// <summary>
    /// Runs an application and returns a <see cref="Task"/> that only completes when the token is triggered or shutdown is triggered.
    /// </summary>
    /// <param name="token">The token to trigger shutdown</param>
    Task RunAsync(CancellationToken token = default);
}