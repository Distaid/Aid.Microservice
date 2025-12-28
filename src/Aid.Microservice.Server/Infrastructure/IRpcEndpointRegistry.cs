using System.Reflection;
using Aid.Microservice.Server.Contracts;

namespace Aid.Microservice.Server.Infrastructure;

public interface IRpcEndpointRegistry
{
    void ScanAssemblies(Assembly assembly);
    bool TryGetMethod(string serviceName, string methodName, out RpcMethodInfo? methodInfo);
    IEnumerable<string> GetRegisteredServices();
}