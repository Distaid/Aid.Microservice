using System.Reflection;

namespace Aid.Microservice.Shared.Attributes;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class RpcCallableAttribute : Attribute
{
    private readonly string? _alias;

    public string MethodName { get; private set; } = null!;

    public RpcCallableAttribute(string? alias = null)
    {
        _alias = alias?.Trim().ToLowerInvariant();
    }

    public void SetMethodName(MethodInfo methodInfo)
    {
        MethodName = !string.IsNullOrEmpty(_alias)
            ? _alias
            : methodInfo.Name.ToLowerInvariant();
    }
}