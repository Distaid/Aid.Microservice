using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Aid.Microservice.Server.Contracts;

namespace Aid.Microservice.Server.Infrastructure;

public interface IRpcEndpointRegistry
{
    [Obsolete("ScanAssemblies is reflection-based and not recommended. Use source-generated endpoints instead.")]
    [RequiresUnreferencedCode("ScanAssemblies uses reflection and is not safe for NativeAOT. Use source-generated endpoints instead.")]
    void ScanAssemblies(Assembly assembly);

    /// <summary>
    /// Registers a source-generated, strongly-typed RPC endpoint without reflection.
    /// </summary>
    void RegisterEndpoint(
        string serviceName,
        string methodName,
        string? exchangeName,
        Type? serializerType,
        RpcMethodInvokerDelegate invoker);

    bool TryGetMethod(string serviceName, string methodName, out RpcMethodInfo? methodInfo);

    IEnumerable<(string ServiceName, string ExchangeName)> GetRegisteredServiceEndpoints();
}