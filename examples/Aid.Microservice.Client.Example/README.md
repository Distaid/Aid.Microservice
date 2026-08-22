# Console Client Example

This example demonstrates how to make RPC calls to microservices from a standalone .NET console application using `RpcClientFactory`.

## Highlights

- **Connection Reuse:** `RpcClientFactory` manages the RabbitMQ connection, reusing it across multiple calls and services.
- **Protocol Flexibility:** Calls standard .NET services (`aid_rpc`), Python Nameko services (`nameko-rpc`), and mixed services.
- **CQRS Queries:** Invokes single-endpoint query handlers using `client.CallQuery<T>()`.
- **Positional & Named Arguments:** Supports `args` and `kwargs` via `RpcNamekoRequest`.

## How to Run

1. Make sure RabbitMQ and the Server example are running.
2. Run the client:
   ```shell
   dotnet run --project examples/Aid.Microservice.Client.Example
   ```
