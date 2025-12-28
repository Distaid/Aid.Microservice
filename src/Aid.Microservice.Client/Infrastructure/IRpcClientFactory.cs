namespace Aid.Microservice.Client.Infrastructure;

public interface IRpcClientFactory
{
    IRpcClient CreateClient(string serviceName);
}