using Aid.Microservice.Client.AspNetCore;
using Aid.Microservice.Client.Infrastructure;
using Aid.Microservice.Generated;
using Aid.Microservice.Shared.Attributes;
using Aid.Microservice.Shared.Protocols;

var builder = WebApplication.CreateBuilder(args);

// 1. Register base RPC client infrastructure (from appsettings.json)
builder.Services.AddAidMicroserviceClient();

// 2. Register source-generated strongly typed client proxies
builder.Services.AddAidMicroserviceGeneratedClients();

var app = builder.Build();

// --- 1. Typed RPC Client (injected via DI, NativeAOT-ready) ---
app.MapGet("/typed/multiple", async (ISimpleClient client) =>
{
    var result = await client.Multiple(6, 7);
    return Results.Ok(new { result });
});

// --- 2. Dynamic RPC call via IRpcClientFactory ---
app.MapGet("/", async (IRpcClientFactory factory) =>
{
    var proxyClient = factory.CreateClient("proxy");
    return await proxyClient.CallAsync<string>("multiplystring");
});

// --- 3. Nameko protocol (Python interop) ---
app.MapGet("/nameko", async (IRpcClientFactory factory) =>
{
    var namekoClient = factory.CreateClient("nameko_service", new NamekoProtocol());
    return await namekoClient.CallAsync<int>("add", new { a = 3, b = 7 });
});

// --- 4. Mixed service: different protocols per call ---
app.MapGet("/mixed", async (IRpcClientFactory factory) =>
{
    var namekoClient = factory.CreateClient("mixed_service", new NamekoProtocol());
    var namekoResult = await namekoClient.CallAsync<int>("nameko_add", new { a = 10, b = 20 });

    var defaultClient = factory.CreateClient("mixed_service");
    var defaultResult = await defaultClient.CallAsync<int>("default_add", new { a = 100, b = 200 });

    return Results.Ok(new { nameko = namekoResult, @default = defaultResult });
});

app.Run();

// Strongly typed client interface definition
[MicroserviceClient("simple")]
public interface ISimpleClient
{
    [RpcCallable("multiple")]
    Task<int> Multiple(int a, int b, CancellationToken cancellationToken = default);
}