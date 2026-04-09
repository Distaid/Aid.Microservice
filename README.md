# Aid.Microservice

[![NuGet Version](https://img.shields.io/nuget/v/Aid.Microservice.Shared.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/Aid.Microservice.Shared/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Aid.Microservice.Shared.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/Aid.Microservice.Shared/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](https://opensource.org/licenses/MIT)

An easy-to-use .NET library for building RPC microservices over RabbitMQ.

This library abstracts away the complex logic of RPC interactions (request/response mapping, timeouts, queue handling) and allows developers to focus on business logic.

# Getting Started

## Server

Create a simple console application:

```shell
dotnet new console -n MyService
```

Then add `appsettings.json`:

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

Create a microservice class:

```csharp
[Microservice] // Service name will be "simple" by default
public class SimpleService
{
    [RpcCallable] // Method name will be "multiple" by default
    public int Multiple(int a, int b) => a * b;
}
```

And run the server in `Program.cs`:

```csharp
using Aid.Microservice.Server;
using Aid.Microservice.Server.Extensions;

var builder = MicroserviceHostBuilder.CreateBuilder(args);

builder.ConfigureServices((_, services) =>
{
    services.AddAidMicroservice(typeof(Program).Assembly);
});

var app = builder.Build();

app.Run();
```

### Features

- **Declarative API** — mark services and methods with `[Microservice]` and `[RpcCallable]` attributes:

```csharp
[Microservice]
public class SimpleService
{
    [RpcCallable]
    public int Multiple(int a, int b) => a * b;
}
```

- **Custom naming** — use aliases for cross-language compatibility:

```csharp
[Microservice("just_name_me")]
public class NamedService
{
    [RpcCallable("and_me")]
    public int Subtract(int a, int b) => a - b;
}
```

- **DI Support** — services are registered as `Scoped`, allowing you to inject DbContext, ILogger, and other dependencies:

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

- **Proxy Support** — use `IRpcProxyFactory` for interservice communication on the server side:

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

- **Async methods** — full async/await support:

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

- **Per-Service Serializers** — assign a custom serializer to an entire service:

```csharp
[Microservice("nameko_service", SerializerType = typeof(NamekoSerializer))]
public class NamekoService
{
    [RpcCallable]
    public int Add(int a, int b) => a + b;
}
```

- **Per-Method Serializers** — mix different serializers within a single service:

```csharp
[Microservice("mixed_service")]
public class MixedService
{
    [RpcCallable("nameko_add", SerializerType = typeof(NamekoSerializer))]
    public int NamekoAdd(int a, int b) => a + b;

    [RpcCallable]
    public int DefaultAdd(int a, int b) => a + b;
}
```

- **Explicit Exchange Names** — control which exchange a service listens on:

```csharp
// Single exchange for all methods
[Microservice("custom_exchange", ExchangeName = "my_custom_rpc")]
public class CustomExchangeService
{
    [RpcCallable]
    public string Echo(string message) => $"Echo: {message}";
}
```

### Protocols & Serializers

The library separates **messaging protocol** (RabbitMQ exchange type, binding) from **message serialization** (body format). This allows fine-grained control over how each service communicates.

#### Architecture

```
IRpcProtocol                    IRequestSerializer
├── ExchangeType (Topic)        ├── CreateRequest()
├── DefaultExchangeName         ├── ParseRequest()
└── DefaultSerializer ──────►   ├── CreateResponse()
                                └── ParseResponse()
```

| Layer          | Interface            | Responsibility                                 |
|----------------|----------------------|------------------------------------------------|
| **Protocol**   | `IRpcProtocol`       | Exchange type, exchange name, routing, binding |
| **Serializer** | `IRequestSerializer` | Body format (JSON structure, args/kwargs)      |

#### Built-in Protocols

| Protocol              | Exchange Type | Exchange Name | Serializer              | Use Case               |
|-----------------------|---------------|---------------|-------------------------|------------------------|
| `DefaultJsonProtocol` | Topic         | `aid_rpc`     | `DefaultJsonSerializer` | .NET ↔ .NET services   |
| `NamekoProtocol`      | Topic         | `nameko-rpc`  | `NamekoSerializer`      | .NET ↔ Python (Nameko) |

> 📘 For a complete reference of all shared models, attributes, and interfaces — see the [Shared Documentation](docs/shared.README.md).

#### Default Protocol

Out of the box, the library uses `DefaultJsonProtocol` with a Topic Exchange.

- **Routing Key**: `service_name.method_name`
- **Payload**: `{"Method": "...", "Parameters": {...}}`
- **Binding**: `service_name.*`

#### Python (Nameko) Interoperability

Built-in support for [Nameko](https://nameko.readthedocs.io/), a popular Python microservices framework. Your .NET services can transparently communicate with Python Nameko services.

```csharp
using Aid.Microservice.Shared.Protocols;

// Server: use NamekoProtocol for all services
builder.Services.AddAidMicroserviceProtocol<NamekoProtocol>();
builder.Services.AddAidMicroservice(typeof(Program).Assembly);

// Client: create a client with NamekoProtocol
var namekoClient = factory.CreateClient("python_service", new NamekoProtocol());
var result = await namekoClient.CallAsync<int>("add", new { a = 1, b = 2 });
```

#### Per-Service and Per-Method Serializers

You can mix different serializers within a single application — or even a single service.

```csharp
// Entire service uses NamekoSerializer on "nameko-rpc" exchange
[Microservice("python_bridge", SerializerType = typeof(NamekoSerializer))]
public class PythonBridge
{
    [RpcCallable]
    public int Add(int a, int b) => a + b;
}

// Mixed service: different methods use different exchanges
[Microservice("mixed")]
public class MixedService
{
    // Listens on "nameko-rpc" exchange
    [RpcCallable("nameko_add", SerializerType = typeof(NamekoSerializer))]
    public int NamekoAdd(int a, int b) => a + b;

    // Listens on "aid_rpc" exchange (default)
    [RpcCallable]
    public int DefaultAdd(int a, int b) => a + b;
}
```

Priority: `[RpcCallable(SerializerType)]` > `[Microservice(SerializerType)]` > protocol's `DefaultSerializer`.

#### Explicit Exchange Names

When a service has methods with different serializers, the server automatically creates queues on all required exchanges. You can also specify exchanges explicitly:

```csharp
// Single exchange for all methods
[Microservice("service", ExchangeName = "my_custom_rpc")]
public class SingleExchangeService { ... }

// Multiple exchanges for different methods
[Microservice("service", Exchanges = ["aid_rpc", "nameko-rpc"])]
public class MultiExchangeService { ... }
```

#### Sending Arguments (args and kwargs)

When using `NamekoSerializer` or `NamekoProtocol`, you can send positional arguments (`args`) alongside named arguments (`kwargs`) via `RpcNamekoRequest`:

```csharp
// kwargs: {'a': 1, 'b': 2}
await client.CallAsync("add", new { a = 1, b = 2 });

// args: [10, 20]
await client.CallAsync("sum", new RpcNamekoRequest(10, 20));

// args=['pdf'], kwargs={'async': true}
await client.CallAsync("generate",
    new RpcNamekoRequest(
        args: new object[] { "pdf" },
        kwargs: new { async = true }
    )
);
```

### Configuration

#### RabbitMQ

RabbitMQ connection is configured via `appsettings.json`:

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

| Option                      | Default        | Description                                                                                      |
|-----------------------------|----------------|--------------------------------------------------------------------------------------------------|
| `ExchangeName`              | (per-protocol) | Global override for all exchanges. Leave empty to use per-protocol defaults.                     |
| `RetryCount`                | 3              | Connection retry attempts.                                                                       |
| `RecoveryInterval`          | 5              | Seconds between retries.                                                                         |
| `DeleteExchangesOnShutdown` | false          | Whether to delete declared exchanges when the server shuts down. Useful for development/cleanup. |

### Protocol Registration

```csharp
using Aid.Microservice.Server.Extensions;
using Aid.Microservice.Shared.Protocols;

var builder = MicroserviceHostBuilder.CreateBuilder(args);

builder.ConfigureServices((context, services) =>
{
    // Optional: replace the default protocol (works before or after AddAidMicroservice)
    services.AddAidMicroserviceProtocol<NamekoProtocol>();

    // Register services from the assembly
    services.AddAidMicroservice(typeof(Program).Assembly);
});

await builder.Build().RunAsync();
```

> 📘 For a complete server reference — including multi-exchange setup, custom serializers, and architecture details — see the [Server Documentation](docs/server.README.md).

You can find the full [Server Example](examples/Aid.Microservice.Server.Example).

## Client

### Standalone (Console App)

Use `RpcClientFactory` to manage connections efficiently. The factory holds the TCP connection, while clients created by it are lightweight.

```csharp
// 1. Create factory (holds the connection)
await using var factory = new RpcClientFactory("localhost", 5672, "guest", "guest");

// 2. Create a client bound to a specific service
await using var simpleClient = factory.CreateClient("simple");

// 3. Make calls
var result = await simpleClient.CallAsync<int>("multiple", new { a = 5, b = 10 });
Console.WriteLine(result);
```

#### Using Different Protocols

```csharp
var factory = new RpcClientFactory("localhost", 5672, "guest", "guest");

// Default protocol (aid_rpc)
var defaultClient = factory.CreateClient("simple");

// Nameko protocol (nameko-rpc)
var namekoClient = factory.CreateClient("python_service", new NamekoProtocol());

// Mixed service — same service, different protocols
var mixedNameko = factory.CreateClient("mixed", new NamekoProtocol());   // → nameko_add
var mixedDefault = factory.CreateClient("mixed");                        // → default_add
```

You can find the full [Client Example](examples/Aid.Microservice.Client.Example).

> 📘 For a complete console client reference — including API details, error handling, and configuration — see the [Client Documentation](docs/client.README.md).

### ASP.NET Core

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

Inject `IRpcClientFactory` into your controllers or minimal API handlers:

```csharp
app.MapGet("/", async (IRpcClientFactory factory) =>
{
    var proxyClient = factory.CreateClient("proxy");
    return await proxyClient.CallAsync<string>("multiplystring");
});

// Nameko protocol
app.MapGet("/nameko", async (IRpcClientFactory factory) =>
{
    var namekoClient = factory.CreateClient("python_service", new NamekoProtocol());
    return await namekoClient.CallAsync<int>("add", new { a = 1, b = 2 });
});
```

You can find the full [Client ASP.NET Core Example](examples/Aid.Microservice.Client.AspNetCore.Example).

> 📘 For a complete ASP.NET Core client reference — including DI registration, controller usage, and error handling — see the [Client ASP.NET Core Documentation](docs/client.aspnetcore.README.md).
