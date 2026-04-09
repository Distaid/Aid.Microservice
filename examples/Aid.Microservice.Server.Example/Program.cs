using Aid.Microservice.Server;
using Aid.Microservice.Server.Extensions;

var builder = MicroserviceHostBuilder.CreateBuilder(args);

builder.ConfigureServices((context, services) =>
{
    // Register protocol before microservices
    // services.AddAidMicroserviceProtocol<NamekoProtocol>();
    
    // Pass assembly for scanning RPC endpoints
    services.AddAidMicroservice(typeof(Program).Assembly);
    
    // Add another for example:
    // services.AddDbContext<AppDbContext>(...);
});

var app = builder.Build();

app.Run();