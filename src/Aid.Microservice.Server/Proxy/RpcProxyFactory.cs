using Aid.Microservice.Client;
using Aid.Microservice.Server.Configuration;
using Microsoft.Extensions.Options;

namespace Aid.Microservice.Server.Proxy;

public class RpcProxyFactory : IRpcProxyFactory
{
    private readonly RabbitMqConfiguration _configuration;
    
    public RpcProxyFactory(IOptions<RabbitMqConfiguration> rabbitMqConfig)
    {
        _configuration = rabbitMqConfig.Value;
    }
    
    public IRpcProxy CreateProxy(string targetServiceName)
    {
        var client = new RpcClient(
            _configuration.Hostname,
            _configuration.Port,
            _configuration.Username,
            _configuration.Password);
        client.InitializeAsync().GetAwaiter().GetResult();
        
        return new RpcProxy(targetServiceName, client);
    }
}