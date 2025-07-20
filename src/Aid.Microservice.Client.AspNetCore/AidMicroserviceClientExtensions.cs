using Aid.Microservice.Client.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aid.Microservice.Client.AspNetCore;

public static class AidMicroserviceClientExtensions
{
    /// <summary>
    /// Adds RpcClient to the container with RabbitMq configuration from appsettings.json.
    /// </summary>
    /// <param name="services">The <see cref="T:Microsoft.Extensions.DependencyInjection.IServiceCollection" /></param>
    /// <returns>The same instance of the <see cref="T:Microsoft.Extensions.DependencyInjection.IServiceCollection" /> for chaining</returns>
    /// <remarks>
    /// Needs RabbitMqConfiguration section in appsettings.json that represent <see cref="Aid.Microservice.Client.Configuration.RabbitMqConfiguration" />. Register as Scoped service.
    /// </remarks>
    public static IServiceCollection AddMicroserviceClient(this IServiceCollection services)
    {
        services.TryAddScoped<RpcClient>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var config = configuration.GetSection(nameof(RabbitMqConfiguration));
            
            return new RpcClient(
                config["Hostname"]!,
                int.Parse(config["Port"]!),
                config["Username"]!,
                config["Password"]!);
        });
        
        return services;
    }
    
    /// <summary>
    /// Adds RpcClient to the container with RabbitMq configuration.
    /// </summary>
    /// <param name="services">The <see cref="T:Microsoft.Extensions.DependencyInjection.IServiceCollection" /></param>
    /// <param name="configuration"><see cref="Aid.Microservice.Client.Configuration.RabbitMqConfiguration" /> instance to initialize connection to RabbitMq</param>
    /// <returns>The same instance of the <see cref="T:Microsoft.Extensions.DependencyInjection.IServiceCollection" /> for chaining</returns>
    /// <remarks>
    /// Register as Scoped service
    /// </remarks>
    public static IServiceCollection AddMicroserviceClient(this IServiceCollection services, RabbitMqConfiguration configuration)
    {
        services.TryAddScoped<RpcClient>(sp => new RpcClient(
            configuration.Hostname,
            configuration.Port,
            configuration.Username,
            configuration.Password)
        );
        
        return services;
    }
}