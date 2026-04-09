# Aid.Microservice.Client

A lightweight RPC client library for communicating with Aid.Microservice services over RabbitMQ.

## Quick Start

```csharp
await using var factory = new RpcClientFactory("localhost", 5672, "guest", "guest");

// Create a client bound to a specific service
await using var client = factory.CreateClient("simple");

// Make a call
var result = await client.CallAsync<int>("multiple", new { a = 5, b = 10 });
Console.WriteLine(result);
```

## API

### IRpcClientFactory

The factory manages a single RabbitMQ connection and creates lightweight clients on demand.

```csharp
// Default protocol (DefaultJsonProtocol)
IRpcClient CreateClient(string serviceName);

// Custom protocol (exchange taken from protocol's DefaultExchangeName)
IRpcClient CreateClient(string serviceName, IRpcProtocol protocol);

// Custom protocol with explicit exchange name
IRpcClient CreateClient(string serviceName, IRpcProtocol protocol, string exchangeName);
```

### IRpcClient

```csharp
// Call with return type
Task<T?> CallAsync<T>(string method, object? parameters = null, TimeSpan? timeout = null, CancellationToken ct = default);

// Call without return type (fire-and-forget style)
Task CallAsync(string method, object? parameters = null, TimeSpan? timeout = null, CancellationToken ct = default);
```

| Parameter           | Required | Default | Description                                              |
|---------------------|----------|---------|----------------------------------------------------------|
| `method`            | Yes      | —       | Method name (as defined by `[RpcCallable]` or its alias) |
| `parameters`        | No       | null    | Anonymous object with named arguments                    |
| `timeout`           | No       | 30s     | Call timeout                                             |
| `cancellationToken` | No       | —       | Cancellation token                                       |

## Protocols

### Default Protocol

```csharp
var client = factory.CreateClient("simple");
// Uses DefaultJsonProtocol on "aid_rpc" exchange
```

### Nameko Protocol (Python Interop)

```csharp
using Aid.Microservice.Shared.Protocols;

var namekoClient = factory.CreateClient("python_service", new NamekoProtocol());
// Uses NamekoSerializer on "nameko-rpc" exchange
```

### Mixed Service — Different Protocols

```csharp
// Same service, different methods on different exchanges
var namekoClient = factory.CreateClient("mixed_service", new NamekoProtocol());
var namekoResult = await namekoClient.CallAsync<int>("nameko_add", new { a = 10, b = 20 });

var defaultClient = factory.CreateClient("mixed_service");
var defaultResult = await defaultClient.CallAsync<int>("default_add", new { a = 100, b = 200 });
```

Clients are cached by `(serviceName, protocol, exchangeName)` — repeated calls with the same parameters reuse the same instance.

## Passing Arguments

### Named Arguments (kwargs)

Default — pass an anonymous object:

```csharp
await client.CallAsync("add", new { a = 1, b = 2 });
// → {"Method": "add", "Parameters": {"a": 1, "b": 2}}
```

### Positional Arguments (args) — Nameko

Use `RpcNamekoRequest` for Nameko-compatible services:

```csharp
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

## Error Handling

```csharp
try
{
    var result = await client.CallAsync<int>("method", new { a = 1 });
}
catch (RpcCallException ex)
{
    // Server-side error — contains error message, type, and CorrelationId
    Console.WriteLine($"RPC Error: {ex.Message}");
}
catch (TimeoutException)
{
    // Call timed out (default: 30s)
    Console.WriteLine("Call timed out");
}
```

## Configuration

### Constructor Overloads

```csharp
// Simple
new RpcClientFactory("localhost", 5672, "guest", "guest");

// With explicit exchange and protocol
new RpcClientFactory("localhost", 5672, "guest", "guest",
    exchangeName: "my_rpc",
    protocol: new NamekoProtocol());

// From configuration object
var config = new RabbitMqConfiguration { ... };
new RpcClientFactory(config);
```

### RabbitMQ Options

| Option             | Default        | Description               |
|--------------------|----------------|---------------------------|
| `ExchangeName`     | (per-protocol) | Global exchange override. |
| `RetryCount`       | 3              | Reconnection attempts.    |
| `RecoveryInterval` | 5              | Seconds between retries.  |
