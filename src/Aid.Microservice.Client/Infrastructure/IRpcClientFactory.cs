using Aid.Microservice.Shared.Interfaces;

namespace Aid.Microservice.Client.Infrastructure;

public interface IRpcClientFactory
{
    /// <summary>
    /// Creates a client using the default protocol and exchange.
    /// </summary>
    IRpcClient CreateClient(string serviceName);

    /// <summary>
    /// Creates a client with a custom protocol. Exchange is taken from the protocol's DefaultExchangeName.
    /// </summary>
    IRpcClient CreateClient(string serviceName, IRpcProtocol protocol);

    /// <summary>
    /// Creates a client with a custom protocol and explicit exchange name.
    /// </summary>
    IRpcClient CreateClient(string serviceName, IRpcProtocol protocol, string exchangeName);
}