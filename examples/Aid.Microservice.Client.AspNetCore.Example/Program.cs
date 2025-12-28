using Aid.Microservice.Client.AspNetCore;
using Aid.Microservice.Client.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAidMicroserviceClient();

var app = builder.Build();

app.MapGet("/", async (IRpcClientFactory factory) =>
{
    var proxyClient = factory.CreateClient("proxy");
    return await proxyClient.CallAsync<string>("multiplystring");
});

app.Run();