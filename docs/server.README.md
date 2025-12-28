# Aid.Microservice.Server

An easy-to-use .NET library for building RPC microservices over RabbitMQ.

This library abstracts away the complex logic of RPC interactions (request/response mapping, timeouts, queue handling) and allows developers to focus on business logic.

## Using

Run server in program.cs:

```csharp
MicroserviceHostBuilder
    .Build(args)
    .Run();
```

## Features

- Attributes for registering services **[Microservice]** and methods **[RpcCallable]**

```csharp
[Microservice]
public class SimpleService
{
    [RpcCallable]
    public int Multiple(int a, int b)
    {
        return a * b;
    }
}
```

- Aliases for services and methods

```csharp
[Microservice("just_name_me")]
public class NamedService
{
    [RpcCallable("and_me")]
    public int Subtract(int a, int b)
    {
        return a - b;
    }
}
```

- DI for component registration in IServiceCollection

```csharp
[Microservice]
public class DiService(ILogger<DiService> logger)
{
    [RpcCallable]
    public void Log()
    {
        logger.LogInformation("DiService called");
    }
}
```

- Fully asynchronous API for maximum performance

```csharp
[Microservice]
public class AsyncService
{
    [RpcCallable]
    public async Task Delay(int seconds)
    {
        await Task.Delay(TimeSpan.FromSeconds(seconds));
    }
}
```

- Proxy for interservice communication via RpcProxyFactory

```csharp
[Microservice]
public class ProxyService
{
    private readonly IRpcProxy _multipleProxy;
    
    public ProxyService(IRpcProxyFactory factory)
    {
        _multipleProxy = factory.CreateProxy("simple");
    }
    
    [RpcCallable]
    public async Task<string> MultiplyString()
    {
        var result = await _multipleProxy.CallAsync<int>("multiple", new { a = 5, b = 6 });
        return $"5 * 6 = {result}";
    }
}
```

## Configuration

### RabbitMq

RabbitMq connection is required in `appsettings.json`

```json
{
    "RabbitMqConfiguration": {
        "Hostname": "localhost",
        "Port": 5672,
        "Username": "user",
        "Password": "12345"
    }
}
```