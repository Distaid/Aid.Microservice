# Aid.Microservice.Server

An easy-to-use .NET library for building RPC microservices over RabbitMQ.

This library abstracts away the complex logic of RPC interactions (request/response mapping, timeouts, queue handling) and allows developers to focus on business logic.

## Using

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