using Aid.Microservice.Client.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aid.Microservice.Client.AspNetCore;

public static class AidMicroserviceClientExtensions
{
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
}