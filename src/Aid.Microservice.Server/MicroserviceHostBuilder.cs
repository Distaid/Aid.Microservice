using Aid.Microservice.Server.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using Aid.Microservice.Server.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Aid.Microservice.Server;

public class MicroserviceHostBuilder : IMicroserviceHostBuilder
{
    private readonly IHostBuilder _hostBuilder;
    private IHost? _host;
    private bool _isBuilt;

    private MicroserviceHostBuilder(string[] args)
    {
        _hostBuilder = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables(prefix: "RPC_");
                config.AddCommandLine(args);
            })
            .ConfigureLogging((hostingContext, logging) =>
            {
                logging.ClearProviders();
                logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                logging.AddAidMicroserviceLogger(hostingContext);
            })
            .ConfigureServices((_, services) =>
            {
                services.AddSerilog();
                services.AddMemoryCache();
                
                var entryAssembly = Assembly.GetEntryAssembly() ?? throw new InvalidOperationException("Could not determine Entry Assembly.");
                services.AddAidMicroservice(entryAssembly);
            });
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Aid.Microservice.Server.MicroserviceHostBuilder" /> class with pre-configured defaults.
    /// Needs configuration in appsettings.json. For example:
    /// <code>
    /// {
    ///     "RabbitMqConfiguration": {
    ///         "Hostname": "localhost",
    ///         "Port": 5672,
    ///         "Username": "guest",
    ///         "Password": "guest"
    ///     }
    /// }
    /// </code>
    /// </summary>
    /// <param name="args">The command line arguments</param>
    /// <returns>The initialized <see cref="Aid.Microservice.Server.Hosting.IMicroserviceHostBuilder" /></returns>
    public static IMicroserviceHostBuilder CreateBuilder(string[] args)
    {
        return new MicroserviceHostBuilder(args);
    }

    /// <inheritdoc/>
    public IMicroserviceHostBuilder ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureDelegate)
    {
        _hostBuilder.ConfigureServices(configureDelegate);
        
        return this;
    }

    /// <inheritdoc/>
    public IMicroserviceHostBuilder Build()
    {
        if (_isBuilt)
        {
            throw new InvalidOperationException("MicroserviceHostBuilder is already built");
        }
        
        _host = _hostBuilder.Build();
        _isBuilt = true;
        return this;
    }

    /// <inheritdoc/>
    public void Run()
    {
        EnsureBuilt();
        _host!.Run();
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken token = default)
    {
        EnsureBuilt();
        await _host!.RunAsync(token).ConfigureAwait(false);
    }
    
    private void EnsureBuilt()
    {
        if (!_isBuilt || _host == null)
        {
            throw new InvalidOperationException("Host is not built. Call Build() before Run()");
        }
    }
}