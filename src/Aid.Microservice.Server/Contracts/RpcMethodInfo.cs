using System.Reflection;

namespace Aid.Microservice.Server.Contracts;

/// <summary>
/// Metadata for a registered RPC endpoint.
/// </summary>
public record RpcMethodInfo(
    Type? ServiceType,
    MethodInfo? Method,
    ParameterInfo[]? Parameters,
    Func<object, object?[], Task<object?>>? FastInvoke,
    Type? SerializerType,
    RpcMethodInvokerDelegate? Invoker = null)
{
    public RpcMethodInfo(
        Type? serializerType,
        RpcMethodInvokerDelegate invoker)
        : this(null, null, null, null, serializerType, invoker)
    {
    }
}