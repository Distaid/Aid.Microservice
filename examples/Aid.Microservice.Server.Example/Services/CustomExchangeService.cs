using Aid.Microservice.Shared.Attributes;

namespace Aid.Microservice.Server.Example.Services;

/// <summary>
/// Service with an explicitly defined exchange name.
/// All methods in this service will listen on "my_custom_rpc" exchange,
/// regardless of their serializer settings.
/// 
/// Useful when you want full control over the RabbitMQ topology.
/// </summary>
[Microservice("custom_exchange", ExchangeName = "my_custom_rpc")]
public class CustomExchangeService
{
    [RpcCallable]
    public string Echo(string message) => $"Echo: {message}";
}
