using System.Collections.Concurrent;
using Aid.Microservice.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aid.Microservice.Server.Infrastructure;

public class SerializerRegistry(IServiceProvider serviceProvider, ILogger<SerializerRegistry> logger)
    : ISerializerRegistry
{
    private readonly ConcurrentDictionary<Type, IRequestSerializer?> _cache = new();

    public IRequestSerializer? GetSerializer(Type? serializerType)
    {
        if (serializerType == null)
        {
            return null;
        }

        return _cache.GetOrAdd(serializerType, type =>
        {
            if (!typeof(IRequestSerializer).IsAssignableFrom(type))
            {
                logger.LogWarning("Type {Type} does not implement IRequestSerializer", type.Name);
                return null;
            }

            try
            {
                var instance = ActivatorUtilities.GetServiceOrCreateInstance(serviceProvider, type);
                if (instance is not IRequestSerializer serializer)
                {
                    logger.LogWarning("Failed to create serializer of type {Type}", type.Name);
                    return null;
                }

                logger.LogDebug("Created serializer of type {Type}", type.Name);
                return serializer;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create serializer of type {Type}", type.Name);
                return null;
            }
        });
    }
}
