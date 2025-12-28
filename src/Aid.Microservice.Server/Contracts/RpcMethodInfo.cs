using System.Reflection;

namespace Aid.Microservice.Server.Contracts;

public record RpcMethodInfo(
    Type ServiceType,
    MethodInfo Method,
    ParameterInfo[] Parameters,
    Func<object, object?[], Task<object?>> FastInvoke);