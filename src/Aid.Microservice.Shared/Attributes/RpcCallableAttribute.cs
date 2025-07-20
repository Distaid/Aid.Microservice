using System.Reflection;

namespace Aid.Microservice.Shared.Attributes;

/// <summary>
/// Attribute that mark method as microservice method.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class RpcCallableAttribute : Attribute
{
    private readonly string? _alias;

    public string MethodName { get; private set; } = null!;

    /// <summary>
    /// Mark method as microservice method.
    /// </summary>
    /// <param name="alias">Name for method. Used name of method as default</param>
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