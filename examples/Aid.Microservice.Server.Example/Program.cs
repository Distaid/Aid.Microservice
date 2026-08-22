using Aid.Microservice.Generated;
using Aid.Microservice.Server;

var builder = MicroserviceHostBuilder.CreateBuilder(args);

builder.ConfigureServices((_, services) =>
{
    // --- Source-Generated NativeAOT Safe Registration ---
    services.AddAidMicroserviceGenerated();
});

var app = builder.Build();

app.Run();