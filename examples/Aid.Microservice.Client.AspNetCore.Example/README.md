# ASP.NET Core Client Example

This example demonstrates how to integrate the RPC client into an ASP.NET Core web application using Dependency Injection.

## Highlights

- **DI Registration:** `builder.Services.AddAidMicroserviceClient()` registers connection lifecycle and `IRpcClientFactory`.
- **Strongly-Typed Clients:** `builder.Services.AddAidMicroserviceGeneratedClients()` registers source-generated typed client proxies (e.g. `ISimpleClient`).
- **Minimal API Endpoints:**
  - `GET /typed/multiple` - Calls RPC via injected typed client (`ISimpleClient`).
  - `GET /` - Calls RPC dynamically via `IRpcClientFactory`.
  - `GET /nameko` - Calls a Python Nameko microservice.
  - `GET /mixed` - Demonstrates calling methods with different protocols on the same service.

## How to Run

1. Make sure RabbitMQ is running.
2. Start the Server example in a separate terminal:
   ```shell
   dotnet run --project examples/Aid.Microservice.Server.Example
   ```
3. Run the ASP.NET Core app:
   ```shell
   dotnet run --project examples/Aid.Microservice.Client.AspNetCore.Example
   ```
4. Navigate to `http://localhost:5000/typed/multiple` or `http://localhost:5000/`.
