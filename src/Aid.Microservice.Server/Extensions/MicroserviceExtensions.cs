using Aid.Microservice.Server.Hosting;
using Aid.Microservice.Server.Infrastructure;
using Aid.Microservice.Server.Proxy;
using Aid.Microservice.Shared.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Reflection;
using Aid.Microservice.Client;
using Aid.Microservice.Client.Infrastructure;
using Aid.Microservice.Shared;
using Aid.Microservice.Shared.Configuration;
using Aid.Microservice.Shared.Interfaces;
using Aid.Microservice.Shared.Protocols;

namespace Aid.Microservice.Server.Extensions;

public static class MicroserviceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAidMicroservice(Assembly assemblyToScan)
        {
            services.AddOptions<RabbitMqConfiguration>()
                .BindConfiguration(nameof(RabbitMqConfiguration))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            
            services.TryAddSingleton<IRabbitMqConnectionService, RabbitMqConnectionService>();
            services.TryAddSingleton<IRpcRequestDispatcher, RpcRequestDispatcher>();
            
            services.TryAddSingleton<IRpcEndpointRegistry>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<RpcEndpointRegistry>>();
                var registry = new RpcEndpointRegistry(logger);
            
                registry.ScanAssemblies(assemblyToScan);
            
                return registry;
            });
            
            RegisterServiceClasses(services, assemblyToScan);
            
            services.TryAddSingleton<IRpcProtocol, DefaultJsonProtocol>();
            services.TryAddSingleton<IRpcProxyFactory, RpcProxyFactory>(); 
            services.TryAddSingleton<IRpcClientFactory, RpcClientFactory>(); 
            
            services.AddHostedService<RpcListenerHost>();
            
            return services;
        }
    }
    
    private static void RegisterServiceClasses(IServiceCollection services, Assembly assembly)
    {
        var serviceTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.GetCustomAttribute<MicroserviceAttribute>() != null);

        foreach (var type in serviceTypes)
        {
            services.TryAddScoped(type);
        }
    }
}