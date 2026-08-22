using System.Collections.Concurrent;
using Aid.Microservice.Shared.Interfaces;
using Aid.Microservice.Shared.Protocols;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aid.Microservice.Server.Infrastructure;

public class SerializerRegistry(IServiceProvider serviceProvider, ILogger<SerializerRegistry> logger)
    : ISerializerRegistry
{
    private readonly ConcurrentDictionary<Type, IRequestSerializer?> _cache = new();

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "Custom serializer instantiation")]
    public IRequestSerializer? GetSerializer(Type? serializerType)
    {
        if (serializerType == null)
        {
            return null;
        }

        return _cache.GetOrAdd(serializerType, type =>
        {
            if (type == typeof(DefaultJsonSerializer)) return new DefaultJsonSerializer();
            if (type == typeof(NamekoSerializer)) return new NamekoSerializer();

            if (!typeof(IRequestSerializer).IsAssignableFrom(type))
            {
                logger.LogWarning("Type {Type} does not implement IRequestSerializer", type.Name);
                return null;
            }

            try
            {
                var fromDi = serviceProvider.GetService(type);
                if (fromDi is IRequestSerializer diSerializer)
                {
                    return diSerializer;
                }

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
