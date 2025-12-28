using Aid.Microservice.Shared.Attributes;

namespace Aid.Microservice.Server.Example.Services;

[Microservice("just_name_me")]
public class NamedService
{
    [RpcCallable("and_me")]
    public int Subtract(int a, int b) => a - b;
}