using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Aid.Microservice.Server.Extensions;

public static class LoggerExtensions
{
    extension(ILoggingBuilder _)
    {
        public void AddAidMicroserviceLogger(HostBuilderContext context)
        {
            var loggerBuilder = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj} env={Environment} {NewLine}{Exception}");

            Log.Logger = loggerBuilder.CreateLogger();
        }
    }
}