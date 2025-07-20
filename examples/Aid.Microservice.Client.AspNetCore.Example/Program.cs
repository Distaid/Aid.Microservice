using Aid.Microservice.Client;
using Aid.Microservice.Client.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMicroserviceClient();

var app = builder.Build();

app.MapGet("/", async (RpcClient client) =>
{
    await client.InitializeAsync();
    return await client.CallAsync<string>("proxy", "multiplystring");
});

app.Run();