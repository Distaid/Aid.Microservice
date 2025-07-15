using Aid.Microservice.Server.Configuration;
using Aid.Microservice.Server.Hosting;
using Aid.Microservice.Server.Infrastructure;
using Aid.Microservice.Server.Proxy;
using Aid.Microservice.Shared.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Aid.Microservice.Server.Extensions;

public static class MicroserviceExtensions
{
    public static IServiceCollection AddMicroservice(this IServiceCollection services, Assembly assemblyToScan)
    {
        var provider = services.BuildServiceProvider();
        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("Default");
        var configuration = provider.GetRequiredService<IConfiguration>();

        var rabbitConfigConfiguration = configuration.GetSection(nameof(RabbitMqConfiguration));
        if (!rabbitConfigConfiguration.Exists())
        {
            throw new Exception("RabbitMqConfiguration section not found");
        }
        services.Configure<RabbitMqConfiguration>(rabbitConfigConfiguration);

        var hostConfiguration = configuration.GetSection(nameof(HostConfiguration));
        if (!hostConfiguration.Exists())
        {
            logger.LogWarning("HostConfiguration section not found. Will use default");
            services.Configure<HostConfiguration>(options =>
            {
                var defaultHostConfiguration = new HostConfiguration();
                options.ShowServiceRegisterMetrics = defaultHostConfiguration.ShowServiceRegisterMetrics;
            });
        }
        else
        {
            services.Configure<HostConfiguration>(hostConfiguration);
        }

        services.TryAddSingleton<IRabbitMqConnectionService, RabbitMqConnectionService>();

        services.AddHostedService<RpcServerHost>();

        RegisterMicroservicesFromAssembly(services, assemblyToScan, logger);

        services.TryAddSingleton<IRpcProxyFactory, RpcProxyFactory>();

        return services;
    }

    private static void RegisterMicroservicesFromAssembly(IServiceCollection services, Assembly assemblyToScan, ILogger logger)
    {
        logger.LogInformation("Registering Microservice from assembly {assemblyToScan}", assemblyToScan.GetName().Name);
        var serviceTypes = assemblyToScan.GetTypes()
            .Select(t => new { Type = t, Attr = t.GetCustomAttribute<MicroserviceAttribute>() })
            .Where(x => x.Attr is not null && x.Type is { IsClass: true, IsAbstract: false })
            .ToList();

        if (serviceTypes.Count == 0)
        {
            logger.LogWarning("Warning: No classes with [Microservice] attribute found in assembly '{assemblyToScan}'", assemblyToScan.GetName().Name);
            return;
        }

        foreach (var serviceInfo in serviceTypes)
        {
            serviceInfo.Attr!.SetServiceName(serviceInfo.Type);
            var serviceName = serviceInfo.Attr!.ServiceName;

            logger.LogInformation(" - Registering {serviceInfo} as service '{serviceName}' (Scoped)", serviceInfo.Type.Name, serviceName);
            services.AddScoped(serviceInfo.Type);
        }
    }
}