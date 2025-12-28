using Aid.Microservice.Client.Infrastructure;
using Aid.Microservice.Shared;
using Aid.Microservice.Shared.Configuration;
using Aid.Microservice.Shared.Interfaces;
using Aid.Microservice.Shared.Protocols;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aid.Microservice.Client.AspNetCore;

public static class AidMicroserviceClientExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds RpcClientFactory to the container with RabbitMq configuration from appsettings.json.
        /// </summary>
        /// <returns>The same instance of the <see cref="T:Microsoft.Extensions.DependencyInjection.IServiceCollection" /> for chaining</returns>
        /// <remarks>
        /// Needs RabbitMqConfiguration section in appsettings.json that represent <see cref="RabbitMqConfiguration" />.
        /// </remarks>
        public IServiceCollection AddAidMicroserviceClient()
        {
            services.AddOptions<RabbitMqConfiguration>()
                .BindConfiguration(nameof(RabbitMqConfiguration))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            RegisterServices(services);

            return services;
        }
        
        /// <summary>
        /// Adds RpcClientFactory to the container with RabbitMq configuration.
        /// </summary>
        /// <param name="services">The <see cref="T:Microsoft.Extensions.DependencyInjection.IServiceCollection" /></param>
        /// <param name="configuration"><see cref="RabbitMqConfiguration" /> instance to initialize connection to RabbitMq</param>
        /// <returns>The same instance of the <see cref="T:Microsoft.Extensions.DependencyInjection.IServiceCollection" /> for chaining</returns>
        public IServiceCollection AddAidMicroserviceClient(RabbitMqConfiguration configuration)
        {
            return AddAidMicroserviceClient(services, options =>
            {
                options.Hostname = configuration.Hostname;
                options.Port = configuration.Port;
                options.Username = configuration.Username;
                options.Password = configuration.Password;
                options.RetryCount = configuration.RetryCount;
                options.RecoveryInterval = configuration.RecoveryInterval;
            });
        }
        
        /// <summary>
        /// Adds RpcClientFactory to the container with RabbitMq configuration.
        /// </summary>
        /// <param name="services">The <see cref="T:Microsoft.Extensions.DependencyInjection.IServiceCollection" /></param>
        /// <param name="configureOptions"><see cref="RabbitMqConfiguration" /> action to initialize connection to RabbitMq</param>
        /// <returns>The same instance of the <see cref="T:Microsoft.Extensions.DependencyInjection.IServiceCollection" /> for chaining</returns>
        public IServiceCollection AddAidMicroserviceClient(Action<RabbitMqConfiguration> configureOptions)
        {
            services.AddOptions<RabbitMqConfiguration>()
                .Configure(configureOptions)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            RegisterServices(services);
        
            return services;
        }
    }
        
    private static void RegisterServices(IServiceCollection services)
    {
        services.TryAddSingleton<IRpcProtocol, DefaultJsonProtocol>();
        services.TryAddSingleton<IRabbitMqConnectionService, RabbitMqConnectionService>();
        services.TryAddSingleton<IRpcClientFactory, RpcClientFactory>();
    }
}