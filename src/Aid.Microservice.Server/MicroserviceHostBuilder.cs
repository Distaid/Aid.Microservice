using Aid.Microservice.Server.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Aid.Microservice.Server;

public class MicroserviceHostBuilder
{
    public static IHost Build(
        string[] args,
        Action<HostBuilderContext, IServiceCollection>? additionalConfigure = null)
    {
        var host = Host.CreateDefaultBuilder(args)
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
                logging.AddMicroserviceLogger(hostingContext);
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddSerilog();
                services.AddMemoryCache();
                services.AddMicroservice(Assembly.GetEntryAssembly()!);

                additionalConfigure?.Invoke(hostContext, services);
            })
            .Build();

        return host;
    }
}