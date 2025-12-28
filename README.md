# Aid.Microservice

[![NuGet Version](https://img.shields.io/nuget/v/Aid.Microservice.Shared.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/Aid.Microservice.Shared/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Aid.Microservice.Shared.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/Aid.Microservice.Shared/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](https://opensource.org/licenses/MIT)

An easy-to-use .NET library for building RPC microservices over RabbitMQ.

This library abstracts away the complex logic of RPC interactions (request/response mapping, timeouts, queue handling) and allows developers to focus on business logic.

# Getting Started

## Server

You can create simple console application

```shell
dotnet new console <name>
```

Then just add `appsettings.json`. For example:

```json
{
    "Logging": {
        "LogLevel": {
            "Default": "Debug",
            "System": "Information",
            "Microsoft": "Information"
        }
    },
    "RabbitMqConfiguration": {
        "Hostname": "localhost",
        "Port": 5672,
        "Username": "guest",
        "Password": "guest",
        "RetryCount": 1
    }
}
```

Create Microservice class:

```csharp
[Microservice] // Service name will be "simple" by default
public class SimpleService
{
    [RpcCallable] // Method name will be "multiple" by default
    public int Multiple(int a, int b)
    {
        return a * b;
    }
}
```

And run server in program.cs:

```csharp
await MicroserviceHostBuilder
    .CreateBuilder(args)
    .Build()
    .RunAsync();
```

### Features

- Attributes for registering services **[Microservice]** and methods **[RpcCallable]**

```csharp
[Microservice]
public class SimpleService
{
    [RpcCallable]
    public int Multiple(int a, int b) => a * b;
}
```

- Aliases for services and methods

```csharp
[Microservice("just_name_me")]
public class NamedService
{
    [RpcCallable("and_me")]
    public int Subtract(int a, int b) => a - b;
}
```

- **DI Support**: Services are registered as `Scoped`, allowing you to inject DbContexts or other dependencies

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

- **Proxy Support**: Use `IRpcProxyFactory` to communicate between services on the server side.

```csharp
[Microservice]
public class ProxyService(IRpcProxyFactory factory)
{
    private readonly IRpcProxy _multipleProxy = factory.CreateProxy("simple");
    
    [RpcCallable]
    public async Task<string> MultiplyString()
    {
        var result = await _multipleProxy.CallAsync<int>("multiple", new { a = 5, b = 6 });
        return $"5 * 6 = {result}";
    }
}
```

### Configuration

#### RabbitMq

RabbitMq connection is required in `appsettings.json`

```json
{
    "RabbitMqConfiguration": {
        "Hostname": "localhost",
        "Port": 5672,
        "Username": "guest",
        "Password": "guest",
        "ExchangeName": "aid_rpc",
        "RetryCount": 3,
        "RecoveryInterval": 5
    }
}
```

- `ExchangeName` (optional): The RabbitMQ exchange used for routing messages. Default is `aid_rpc`.
- `RetryCount` (optional): How many times to retry connection. Default is 3.
- `RecoveryInterval` (optional): Seconds between retries. Default is 5.

#### Host

Simple example of creating host:

```csharp
await MicroserviceHostBuilder
    .CreateBuilder(args)
    .Build()
    .RunAsync();
```

You can watch the [Server Example Project](examples/Aid.Microservice.Server.Example).

## Client

#### Standalone (Console App)

For console applications, use `RpcClientFactory` to manage connections efficiently. The factory holds the TCP connection, while clients created by it are lightweight.

```csharp
// 1. Create factory (holds the connection)
await using var rpcFactory = new RpcClientFactory("localhost", 5672, "guest", "guest");

// 2. Create a client bound to a specific service
await using var simpleClient = factory.CreateClient("simple");

// 3. Make calls
try 
{
    var simpleResult = await simpleClient.CallAsync<int>("multiple", new {a = 5, b = 10});
    Console.WriteLine(simpleResult);
}
catch (RpcCallException ex)
{
    Console.WriteLine($"RPC Error: {ex.Message}");
}
```

You can watch the [Client Example Project](examples/Aid.Microservice.Client.Example).

### AspNetCore

#### Configuration

Register the client infrastructure in `Program.cs`. This registers `IRpcClientFactory` as a Singleton:

```csharp
// Loads configuration from "RabbitMqConfiguration" section
builder.Services.AddAidMicroserviceClient();
```

Or pass configuration manually:

```csharp
builder.Services.AddAidMicroserviceClient(config => 
{
    config.Hostname = "localhost";
    config.ExchangeName = "my_custom_rpc";
});
```

#### Usage

Inject `IRpcClientFactory` into your controllers or services.

```csharp
app.MapGet("/", async (IRpcClientFactory factory) =>
{
    await using var proxyClient = factory.CreateClient("proxy");
    return await proxyClient.CallAsync<string>("multiplystring");
});
```

You can watch the [Client AspNetCore Example Project](examples/Aid.Microservice.Client.AspNetCore.Example).