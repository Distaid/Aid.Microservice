using Aid.Microservice.Shared.Attributes;

namespace Aid.Microservice.Server.Example.Services;

/// <summary>
/// Demonstrates custom naming of services and methods.
/// Service is registered as "just_name_me" instead of "named".
/// Method is registered as "and_me" instead of "subtract".
/// 
/// Useful for cross-language compatibility or migration scenarios.
/// </summary>
[Microservice("just_name_me")]
public class NamedService
{
    [RpcCallable("and_me")]
    public int Subtract(int a, int b) => a - b;
}