# Server Example

This example demonstrates how to host RPC services and CQRS query handlers with compile-time source generation and NativeAOT readiness.

## Highlights

- **Compile-Time Discovery:** Uses `services.AddAidMicroserviceGenerated()` to register services and endpoints without runtime reflection.
- **Multiple Service Styles:**
  - Standard RPC services (`SimpleService`, `AsyncService`, `DiService`).
  - CQRS single-endpoint query handlers (`GetProductQueryHandler`, `ClearCacheQueryHandler`).
  - Python / Nameko interop (`NamekoService`, `MixedService`).
  - Custom exchange routing (`CustomExchangeService`).

## How to Run

1. Make sure RabbitMQ is running (e.g. `localhost:5672`).
2. Adjust `appsettings.json` if needed:
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
3. Run the application:
   ```shell
   dotnet run --project examples/Aid.Microservice.Server.Example
   ```
