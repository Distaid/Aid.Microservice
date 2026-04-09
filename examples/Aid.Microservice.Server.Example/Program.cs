using Aid.Microservice.Server;
using Aid.Microservice.Server.Extensions;

var builder = MicroserviceHostBuilder.CreateBuilder(args);

builder.ConfigureServices((_, services) =>
{
    // --- Protocol Registration (optional) ---
    // Replace the default protocol for the entire server.
    // Use this if ALL services should use a different protocol (e.g. Nameko for Python interop).
    //
    // services.AddAidMicroserviceProtocol<NamekoProtocol>();
    //
    // NOTE: Can be called before or after AddAidMicroservice — order does not matter.

    // --- Microservice Registration ---
    // Scans the assembly for [Microservice] and [RpcCallable] attributes.
    services.AddAidMicroservice(typeof(Program).Assembly);

    // --- Custom DI Registrations ---
    // services.AddDbContext<AppDbContext>(...);
});

var app = builder.Build();

app.Run();