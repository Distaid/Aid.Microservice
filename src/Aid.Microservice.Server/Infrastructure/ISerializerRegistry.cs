using Aid.Microservice.Shared.Interfaces;

namespace Aid.Microservice.Server.Infrastructure;

/// <summary>
/// Registry of serializers. Resolves an <see cref="IRequestSerializer"/> instance by type.
/// </summary>
public interface ISerializerRegistry
{
    /// <summary>
    /// Gets or creates a serializer of the specified type.
    /// </summary>
    /// <param name="serializerType">A type that implements <see cref="IRequestSerializer"/>.</param>
    /// <returns>The serializer instance, or null if the type is not recognized.</returns>
    IRequestSerializer? GetSerializer(Type? serializerType);
}
