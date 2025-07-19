# Aid.Microservice

[![NuGet Version](https://img.shields.io/nuget/v/Aid.Microservice.Shared.svg)](https://www.nuget.org/packages/Aid.Microservice.Shared/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

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
        "Username": "user",
        "Password": "12345",
        "RetryCount": 1
    },
    "HostConfiguration": {
        "ShowServiceRegisterMetrics": false
    }
}
```

Create Microservice class:

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

And run server in program.cs:

```csharp
MicroserviceHostBuilder
    .Build(args)
    .Run();
```

### Features

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

### Configuration

#### RabbitMq

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

Optionally you can add `RetryCount` to set how many times to reconnect and `RecoveryInterval` to set interval between retry.

> Note: `RetryCount` is 3 by default. `RecoveryInterval` is 5 by default and sets in seconds.

#### Host

Host configuration is optional and sets whether to output logs when registering services:

```json
{
    "HostConfiguration": {
        "ShowServiceRegisterMetrics": false
    }
}
```

> Note: `ShowServiceRegisterMetrics` is **true** by default.

You can watch the [Server Example Project](examples/Aid.Microservice.Server.Example).

## Client

To access the server you need RpcClient:

```csharp
await using var client = new RpcClient("localhost", 5672, "user", "12345");
await client.InitializeAsync();
```

To make request call CallAsync or CallAsync<> with return type:

```csharp
await client.CallAsync<int>("simple", "multiple", new {a = 5, b = 10});
```

CallAsync accept 5 arguments:
- Service (string, required)
- Method (string, required)
- Parameters (object, optional)
- Timeout (TimeSpan, optional)
- CancellationToken (CancellationToken, optional)

You can watch the [Client Example Project](examples/Aid.Microservice.Client.Example).