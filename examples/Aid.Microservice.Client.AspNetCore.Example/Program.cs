using Aid.Microservice.Client.AspNetCore;
using Aid.Microservice.Client.Infrastructure;
using Aid.Microservice.Shared.Protocols;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAidMicroserviceClient();

var app = builder.Build();

// --- Default protocol (DefaultJsonProtocol on "aid_rpc") ---
app.MapGet("/", async (IRpcClientFactory factory) =>
{
    var proxyClient = factory.CreateClient("proxy");
    return await proxyClient.CallAsync<string>("multiplystring");
});

// --- Nameko protocol (NamekoSerializer on "nameko-rpc") ---
app.MapGet("/nameko", async (IRpcClientFactory factory) =>
{
    var namekoClient = factory.CreateClient("nameko_service", new NamekoProtocol());
    return await namekoClient.CallAsync<int>("add", new { a = 3, b = 7 });
});

// --- Mixed service: different protocols per call ---
app.MapGet("/mixed", async (IRpcClientFactory factory) =>
{
    var namekoClient = factory.CreateClient("mixed_service", new NamekoProtocol());
    var namekoResult = await namekoClient.CallAsync<int>("nameko_add", new { a = 10, b = 20 });

    var defaultClient = factory.CreateClient("mixed_service");
    var defaultResult = await defaultClient.CallAsync<int>("default_add", new { a = 100, b = 200 });

    return new { nameko = namekoResult, @default = defaultResult };
});

app.Run();