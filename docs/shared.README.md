# Aid.Microservice.Shared

Shared models, attributes, and protocols for the Aid.Microservice ecosystem.

This package is referenced by both `Aid.Microservice.Server` and `Aid.Microservice.Client` and does not need to be installed directly in most cases.

## Attributes

### `[Microservice]`

Marks a class as an RPC service.

```csharp
[Microservice]                                             // name: class name without "Service" suffix
[Microservice("custom_name")]                              // explicit name
[Microservice(SerializerType = typeof(NamekoSerializer))]  // per-service serializer
[Microservice(ExchangeName = "my_rpc")]                    // explicit exchange
[Microservice(Exchanges = ["aid_rpc", "nameko-rpc"])]      // multiple exchanges
```

### `[RpcCallable]`

Marks a method as an RPC endpoint.

```csharp
[RpcCallable]                                            // name: method name (lowercase)
[RpcCallable("alias")]                                   // explicit name
[RpcCallable(SerializerType = typeof(NamekoSerializer))] // per-method serializer
```

### `[MicroserviceQuery]`

Marks a class as a single-endpoint Microservice Query or Command handler (CQRS style).

```csharp
[MicroserviceQuery]                                             // query name: class name minus suffixes (e.g. "Query", "QueryHandler", "Command")
[MicroserviceQuery("custom_query")]                             // explicit query name
[MicroserviceQuery(SerializerType = typeof(NamekoSerializer))]  // custom serializer
[MicroserviceQuery(ExchangeName = "my_rpc")]                    // explicit exchange
```

## Protocols

### `IRpcProtocol`

Defines the messaging protocol (exchange type, exchange name, serializer).

| Property              | Description                                  |
|-----------------------|----------------------------------------------|
| `ExchangeType`        | RabbitMQ exchange type (Topic, Direct, etc.) |
| `DefaultExchangeName` | Suggested exchange name                      |
| `DefaultSerializer`   | `IRequestSerializer` used for message body   |

Built-in implementations:
- `DefaultJsonProtocol` — .NET ↔ .NET (Topic, `aid_rpc`)
- `NamekoProtocol` — .NET ↔ Python Nameko (Topic, `nameko-rpc`)

### `IRequestSerializer`

Handles serialization/deserialization of RPC message bodies.

| Member             | Description                                 |
|--------------------|---------------------------------------------|
| `ContentType`      | MIME type (e.g. `application/json`)         |
| `ExchangeName`     | Suggested exchange name for this serializer |
| `CreateRequest()`  | Serialize request body                      |
| `ParseRequest()`   | Deserialize incoming request                |
| `CreateResponse()` | Serialize response body                     |
| `ParseResponse()`  | Deserialize response body                   |

Built-in implementations:
- `DefaultJsonSerializer` — `{"Method": "...", "Parameters": {...}}`
- `NamekoSerializer` — `{"args": [...], "kwargs": {...}, "context_data": {...}}`

## Models

### `RpcRequest`

Parsed incoming request.

```csharp
public record RpcRequest
{
    public string Method { get; init; }
    public Dictionary<string, JsonElement>? Parameters { get; init; }
}
```

### `RpcResponse`

RPC call result.

```csharp
public record RpcResponse
{
    public object? Result { get; init; }
    public RpcError? Error { get; set; }
    public bool IsSuccess => Error == null;
}
```

### `RpcError`

Error details from server-side execution.

```csharp
public record RpcError
{
    public string Message { get; }
    public string? StackTrace { get; }
    public string? ErrorType { get; }
}
```

### `RpcNamekoRequest`

Wrapper for sending positional arguments to Nameko-compatible services.

```csharp
new RpcNamekoRequest(1, 2, 3)                              // args only
new RpcNamekoRequest(args: [1, 2], kwargs: new { x = 10 }) // both
```

## Configuration

### `RabbitMqConfiguration`

```csharp
public class RabbitMqConfiguration
{
    public string Hostname { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string? ExchangeName { get; set; }
    public int RetryCount { get; set; } = 3;
    public int RecoveryInterval { get; set; } = 5;
    public bool DeleteExchangesOnShutdown { get; set; }
    public bool DeleteQueuesOnShutdown { get; set; }
}
```
