# Aid.Microservice.Server

A .NET library for building RPC microservices over RabbitMQ with support for multiple protocols and per-method serializers.

## Quick Start

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

Add `appsettings.json` with RabbitMQ connection:

```json
{
    "RabbitMqConfiguration": {
        "Hostname": "localhost",
        "Port": 5672,
        "Username": "guest",
        "Password": "guest"
    }
}
```

## Features

### Declarative API

Mark classes and methods with `[Microservice]` and `[RpcCallable]`:

```csharp
[Microservice]
public class SimpleService
{
    [RpcCallable]
    public int Multiple(int a, int b) => a * b;
}
```

Service name is inferred from the class name (`SimpleService` → `simple`). Method name is inferred from the method name.

### Custom Naming

Use aliases for cross-language compatibility:

```csharp
[Microservice("just_name_me")]
public class NamedService
{
    [RpcCallable("and_me")]
    public int Subtract(int a, int b) => a - b;
}
```

### Dependency Injection

Services are registered as `Scoped`. Inject any registered dependency:

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

### Async Methods

Full async/await support, including `Task`, `Task<T>`, `ValueTask`, and `ValueTask<T>`:

```csharp
[Microservice]
public class AsyncService
{
    [RpcCallable]
    public async Task Delay(int seconds)
    {
        await Task.Delay(TimeSpan.FromSeconds(seconds));
    }

    [RpcCallable]
    public async ValueTask<string> GetData(string id)
    {
        await Task.Yield();
        return $"Data: {id}";
    }
}
```

### Proxy Support

Call other services from within a service using `IRpcProxyFactory`:

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

### Per-Service Serializers

Assign a custom serializer to an entire service:

```csharp
[Microservice("python_bridge", SerializerType = typeof(NamekoSerializer))]
public class PythonBridge
{
    [RpcCallable]
    public int Add(int a, int b) => a + b;
}
```

All methods in this service will use `NamekoSerializer` and listen on the `nameko-rpc` exchange (inferred from the serializer's `ExchangeName`).

### Per-Method Serializers

Mix different serializers within a single service:

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

The server automatically detects all unique exchanges used by the service and creates a queue on each one.

Priority: `[RpcCallable(SerializerType)]` > `[Microservice(SerializerType)]` > protocol's `DefaultSerializer`.

### Explicit Exchange Names

Control which exchange a service listens on:

```csharp
// Single exchange for all methods
[Microservice("custom", ExchangeName = "my_custom_rpc")]
public class CustomExchangeService { ... }

// Multiple exchanges for different methods
[Microservice("multi", Exchanges = ["aid_rpc", "nameko-rpc"])]
public class MultiExchangeService { ... }
```

### Global Exchange Override

Set `ExchangeName` in `RabbitMqConfiguration` to force all services onto a single exchange:

```json
{
    "RabbitMqConfiguration": {
        "Hostname": "localhost",
        "ExchangeName": "all_in_one"
    }
}
```

## Protocols & Serializers

### Architecture

The library separates **messaging protocol** from **message serialization**:

| Layer          | Interface            | Responsibility                                 |
|----------------|----------------------|------------------------------------------------|
| **Protocol**   | `IRpcProtocol`       | Exchange type, exchange name, routing, binding |
| **Serializer** | `IRequestSerializer` | Body format (JSON structure, args/kwargs)      |

### Built-in Protocols

| Protocol              | Exchange Type | Exchange Name | Serializer              |
|-----------------------|---------------|---------------|-------------------------|
| `DefaultJsonProtocol` | Topic         | `aid_rpc`     | `DefaultJsonSerializer` |
| `NamekoProtocol`      | Topic         | `nameko-rpc`  | `NamekoSerializer`      |

### Replacing the Default Protocol

```csharp
using Aid.Microservice.Server.Extensions;
using Aid.Microservice.Shared.Protocols;

var builder = MicroserviceHostBuilder.CreateBuilder(args);

builder.ConfigureServices((context, services) =>
{
    // Replace default protocol (order does not matter)
    services.AddAidMicroserviceProtocol<NamekoProtocol>();

    services.AddAidMicroservice(typeof(Program).Assembly);
});
```

### Custom Serializer

Implement `IRequestSerializer` for your own message format:

```csharp
public class MySerializer : IRequestSerializer
{
    public string ContentType => "application/json";
    public string? ExchangeName => "my_exchange";

    public byte[] CreateRequest(string serviceName, string methodName, object? parameters, JsonSerializerOptions options)
    {
        // Build your message format
    }

    public RpcRequest ParseRequest(ReadOnlySpan<byte> body, string routingKey, JsonSerializerOptions options)
    {
        // Parse incoming message
    }

    public byte[] CreateResponse(RpcResponse response, JsonSerializerOptions options) { ... }
    public RpcResponse ParseResponse(ReadOnlySpan<byte> body, JsonSerializerOptions options) { ... }
}
```

Register and use it:

```csharp
[Microservice("my_service", SerializerType = typeof(MySerializer))]
public class MyService { ... }
```

## Configuration

### RabbitMQ

```json
{
    "RabbitMqConfiguration": {
        "Hostname": "localhost",
        "Port": 5672,
        "Username": "guest",
        "Password": "guest",
        "ExchangeName": "",
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
| `DeleteQueuesOnShutdown`    | false          | Whether to delete declared queues when the server shuts down. Useful for development/cleanup.    |

## Architecture Overview

```
RpcListenerHost
    │
    ├── For each (service, exchange) pair:
    │       ├── Declare exchange (deduplicated)
    │       ├── Declare queue: {exchange}_{service}
    │       └── Bind queue: service_name.*
    │
    └── On message received:
            ├── ResolveSerializer(serviceName, routingKey)
            │       ├── [RpcCallable(SerializerType)] → resolve
            │       ├── [Microservice(SerializerType)] → resolve
            │       └── fallback → protocol.DefaultSerializer
            ├── serializer.ParseRequest() → RpcRequest
            ├── RpcRequestDispatcher.DispatchAsync() → execute method
            └── serializer.CreateResponse() → send reply
```
