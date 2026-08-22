namespace Aid.Microservice.SourceGenerators.Models;

public sealed class RpcClientInterfaceModel(
    string interfaceName,
    string fullInterfaceName,
    string @namespace,
    string serviceName,
    string? explicitExchangeName,
    string? customSerializerTypeFullName,
    System.Collections.Generic.List<RpcClientMethodModel> methods)
{
    public string InterfaceName { get; } = interfaceName;
    public string FullInterfaceName { get; } = fullInterfaceName;
    public string Namespace { get; } = @namespace;
    public string ServiceName { get; } = serviceName;
    public string? ExplicitExchangeName { get; } = explicitExchangeName;
    public string? CustomSerializerTypeFullName { get; } = customSerializerTypeFullName;
    public System.Collections.Generic.List<RpcClientMethodModel> Methods { get; } = methods;
}

public sealed class RpcClientMethodModel(
    string methodName,
    string rpcAlias,
    bool isQuery,
    string returnTypeFullName,
    bool isAsync,
    bool isValueTask,
    bool returnsVoid,
    string? resultTypeFullName,
    System.Collections.Generic.List<RpcClientParameterModel> parameters)
{
    public string MethodName { get; } = methodName;
    public string RpcAlias { get; } = rpcAlias;
    public bool IsQuery { get; } = isQuery;
    public string ReturnTypeFullName { get; } = returnTypeFullName;
    public bool IsAsync { get; } = isAsync;
    public bool IsValueTask { get; } = isValueTask;
    public bool ReturnsVoid { get; } = returnsVoid;
    public string? ResultTypeFullName { get; } = resultTypeFullName;
    public System.Collections.Generic.List<RpcClientParameterModel> Parameters { get; } = parameters;
}

public sealed class RpcClientParameterModel(
    string name,
    string typeFullName,
    bool isCancellationToken,
    bool isTimeout,
    bool hasDefaultValue,
    string? defaultValueLiteral)
{
    public string Name { get; } = name;
    public string TypeFullName { get; } = typeFullName;
    public bool IsCancellationToken { get; } = isCancellationToken;
    public bool IsTimeout { get; } = isTimeout;
    public bool HasDefaultValue { get; } = hasDefaultValue;
    public string? DefaultValueLiteral { get; } = defaultValueLiteral;
}
