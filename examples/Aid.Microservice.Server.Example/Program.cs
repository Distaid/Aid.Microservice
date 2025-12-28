using Aid.Microservice.Server;

await MicroserviceHostBuilder
    .CreateBuilder(args)
    .ConfigureServices((context, services) => 
    {
        // For example:
        // services.AddDbContext<AppDbContext>(...);
    })
    .Build()
    .RunAsync();