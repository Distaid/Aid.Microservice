# Aid.Microservice.Client.AspNetCore

ASP.NET Core integration for the Aid.Microservice RPC client. Provides DI-based registration and a shared connection pool for your web application.

## Quick Start

### 1. Register the Client

```csharp
using Aid.Microservice.Client.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Loads "RabbitMqConfiguration" from appsettings.json
builder.Services.AddAidMicroserviceClient();

var app = builder.Build();
```

### 2. Use in Endpoints

```csharp
app.MapGet("/", async (IRpcClientFactory factory) =>
{
    var client = factory.CreateClient("simple");
    return await client.CallAsync<int>("multiple", new { a = 5, b = 10 });
});
```

### 3. Use in Controllers

```csharp
[ApiController]
[Route("api/[controller]")]
public class RpcController(IRpcClientFactory factory) : ControllerBase
{
    [HttpGet("multiply")]
    public async Task<IActionResult> Multiply(int a, int b)
    {
        var client = factory.CreateClient("simple");
        var result = await client.CallAsync<int>("multiple", new { a, b });
        return Ok(result);
    }
}
```

## Configuration

### From appsettings.json (recommended)

```json
{
    "RabbitMqConfiguration": {
        "Hostname": "localhost",
        "Port": 5672,
        "Username": "guest",
        "Password": "guest",
        "RetryCount": 3,
        "RecoveryInterval": 5
    }
}
```

```csharp
builder.Services.AddAidMicroserviceClient();
```

### From a Configuration Object

```csharp
var config = new RabbitMqConfiguration
{
    Hostname = "rabbitmq",
    Port = 5672,
    Username = "user",
    Password = "secret"
};

builder.Services.AddAidMicroserviceClient(config);
```

### From a Configuration Action

```csharp
builder.Services.AddAidMicroserviceClient(options =>
{
    options.Hostname = builder.Configuration["RABBIT_HOST"] ?? "localhost";
    options.Port = 5672;
    options.Username = "guest";
    options.Password = "guest";
});
```

## Using Different Protocols

### Default Protocol

```csharp
// Uses DefaultJsonProtocol on "aid_rpc" exchange
var client = factory.CreateClient("simple");
var result = await client.CallAsync<int>("multiple", new { a = 5, b = 10 });
```

### Nameko Protocol (Python Interop)

```csharp
using Aid.Microservice.Shared.Protocols;

var namekoClient = factory.CreateClient("python_service", new NamekoProtocol());
var result = await namekoClient.CallAsync<int>("add", new { a = 1, b = 2 });
```

### Mixed Service — Different Protocols per Call

```csharp
app.MapGet("/mixed", async (IRpcClientFactory factory) =>
{
    // Call nameko_add on "nameko-rpc" exchange
    var namekoClient = factory.CreateClient("mixed_service", new NamekoProtocol());
    var namekoResult = await namekoClient.CallAsync<int>("nameko_add", new { a = 10, b = 20 });

    // Call default_add on "aid_rpc" exchange
    var defaultClient = factory.CreateClient("mixed_service");
    var defaultResult = await defaultClient.CallAsync<int>("default_add", new { a = 100, b = 200 });

    return new { nameko = namekoResult, @default = defaultResult };
});
```

Clients are cached by `(serviceName, protocol, exchangeName)` — repeated calls with the same parameters reuse the same instance.

## Error Handling

```csharp
app.MapGet("/call", async (IRpcClientFactory factory) =>
{
    try
    {
        var client = factory.CreateClient("simple");
        var result = await client.CallAsync<int>("method", new { a = 1 });
        return Results.Ok(result);
    }
    catch (RpcCallException ex)
    {
        // Server-side error — contains error message, type, and CorrelationId
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (TimeoutException)
    {
        // Call timed out (default: 30s)
        return Results.StatusCode(504);
    }
});
```

## Architecture

```
AddAidMicroserviceClient()
    │
    ├── Registers IRpcClientFactory as Singleton
    │   ├── Holds one RabbitMQ connection for the entire app
    │   └── Creates lightweight RpcClient per (service, protocol, exchange)
    │
    ├── Registers IRpcProtocol → DefaultJsonProtocol
    │
    └── Registers IRabbitMqConnectionService
        ├── Reads RabbitMqConfiguration from appsettings.json
        └── Manages connection lifecycle (connect, reconnect, dispose)
```

## Passing Arguments

### Named Arguments (kwargs) — Default

```csharp
await client.CallAsync("add", new { a = 1, b = 2 });
```

### Positional Arguments (args) — Nameko

```csharp
await client.CallAsync("sum", new RpcNamekoRequest(10, 20));

await client.CallAsync("generate",
    new RpcNamekoRequest(
        args: new object[] { "pdf" },
        kwargs: new { async = true }
    )
);
```

## Connection Lifecycle

The connection is managed automatically:

- **Created** on first `CreateClient()` call or first `CallAsync()`
- **Reused** across all clients in the application
- **Disposed** when the application shuts down

No manual connection handling is required.
