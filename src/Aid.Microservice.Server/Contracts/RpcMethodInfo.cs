using System.Reflection;

namespace Aid.Microservice.Server.Contracts;

/// <summary>
/// Metadata for a registered RPC endpoint.
/// </summary>
/// <param name="ServiceType">The type of the service containing the method.</param>
/// <param name="Method">The <see cref="MethodInfo"/> of the RPC endpoint.</param>
/// <param name="Parameters">Parameter information for method invocation.</param>
/// <param name="FastInvoke">Compiled delegate for fast method invocation.</param>
/// <param name="SerializerType">Optional custom serializer type for this endpoint.</param>
public record RpcMethodInfo(
    Type ServiceType,
    MethodInfo Method,
    ParameterInfo[] Parameters,
    Func<object, object?[], Task<object?>> FastInvoke,
    Type? SerializerType);