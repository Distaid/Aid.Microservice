namespace Aid.Microservice.Shared.Attributes;

/// <summary>
/// Marks an interface as a typed RPC client for a target microservice.
/// The Source Generator will generate a strongly-typed, NativeAOT-safe client implementation.
/// </summary>
[AttributeUsage(AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
public class MicroserviceClientAttribute(string serviceName) : Attribute
{
    public string ServiceName { get; } = serviceName.Trim().ToLowerInvariant();
    public string? ExchangeName { get; init; }
    public Type? SerializerType { get; init; }
}
