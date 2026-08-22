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
        /// <summary>
        /// Registers core Aid.Microservice server infrastructure.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "RabbitMqConfiguration binding is handled via ConfigurationBindingGenerator")]
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "RabbitMqConfiguration binding is handled via ConfigurationBindingGenerator")]
        public IServiceCollection AddAidMicroserviceCore()
        {
            services.AddOptions<RabbitMqConfiguration>()
                .BindConfiguration(nameof(RabbitMqConfiguration))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.TryAddSingleton<IRabbitMqConnectionService, RabbitMqConnectionService>();
            services.TryAddSingleton<IRpcRequestDispatcher, RpcRequestDispatcher>();
            services.TryAddSingleton<ISerializerRegistry, SerializerRegistry>();
            services.TryAddSingleton<IRpcEndpointRegistry, RpcEndpointRegistry>();
            services.TryAddSingleton<IRpcProtocol, DefaultJsonProtocol>();
            services.TryAddSingleton<IRpcProxyFactory, RpcProxyFactory>();
            services.TryAddSingleton<IRpcClientFactory, RpcClientFactory>();

            services.AddHostedService<RpcListenerHost>();

            return services;
        }

        /// <summary>
        /// Scan assembly and register RPC endpoints (Reflection-based, not recommended).
        /// </summary>
        /// <param name="assemblyToScan">Assembly to scan</param>
        /// <returns>The same instance of the <see cref="T:Microsoft.Extensions.DependencyInjection.IServiceCollection" /> for chaining</returns>
        [Obsolete("AddAidMicroservice(Assembly) is reflection-based and not recommended. Use services.AddAidMicroserviceGenerated() from Aid.Microservice.Generated for compile-time registration and NativeAOT compatibility.")]
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("This method uses reflection to scan assemblies and is not safe for NativeAOT. Use source-generated registration instead.")]
        public IServiceCollection AddAidMicroservice(Assembly assemblyToScan)
        {
            services.AddAidMicroserviceCore();

            services.Replace(ServiceDescriptor.Singleton<IRpcEndpointRegistry>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<RpcEndpointRegistry>>();
                var serializerRegistry = sp.GetRequiredService<ISerializerRegistry>();
                var protocol = sp.GetRequiredService<IRpcProtocol>();
                var registry = new RpcEndpointRegistry(logger, serializerRegistry, protocol);

                registry.ScanAssemblies(assemblyToScan);

                return registry;
            }));

            RegisterServiceClasses(services, assemblyToScan);

            return services;
        }

        /// <summary>
        /// Register RPC protocol. Can be called before or after <see cref="AddAidMicroservice"/>.
        /// </summary>
        /// <typeparam name="TProtocol">Realization of <see cref="IRpcProtocol"/> interface</typeparam>
        /// <returns>The same instance of the <see cref="T:Microsoft.Extensions.DependencyInjection.IServiceCollection" /> for chaining</returns>
        public IServiceCollection AddAidMicroserviceProtocol<TProtocol>()
            where TProtocol : class, IRpcProtocol
        {
            services.RemoveAll<IRpcProtocol>();
            services.AddSingleton<IRpcProtocol, TProtocol>();

            return services;
        }
    }

    private static void RegisterServiceClasses(IServiceCollection services, Assembly assembly)
    {
        var serviceTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } &&
                (t.GetCustomAttribute<MicroserviceAttribute>() != null || t.GetCustomAttribute<MicroserviceQueryAttribute>() != null));

        foreach (var type in serviceTypes)
        {
            services.TryAddScoped(type);
        }
    }
}