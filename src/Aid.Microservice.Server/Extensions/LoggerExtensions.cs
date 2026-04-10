using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Aid.Microservice.Server.Extensions;

public static class LoggerExtensions
{
    extension(ILoggingBuilder _)
    {
        public void AddAidMicroserviceLogger(HostBuilderContext context)
        {
            var loggerConfig = new LoggerConfiguration()
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj} env={Environment} {NewLine}{Exception}");

            ApplyLoggingSection(loggerConfig, context.Configuration.GetSection("Logging"));

            Log.Logger = loggerConfig.CreateLogger();
        }

        private static void ApplyLoggingSection(LoggerConfiguration config, IConfigurationSection loggingSection)
        {
            if (!loggingSection.Exists())
            {
                return;
            }

            var logLevelSection = loggingSection.GetSection("LogLevel");
            if (!logLevelSection.Exists())
            {
                return;
            }

            foreach (var child in logLevelSection.GetChildren())
            {
                if (LogLevelParser.TryParse(child.Value, out var level))
                {
                    if (child.Key == "Default")
                    {
                        config.MinimumLevel.Is(level);
                    }
                    else
                    {
                        config.MinimumLevel.Override(child.Key, level);
                    }
                }
            }
        }
    }

    private static class LogLevelParser
    {
        private static readonly Dictionary<string, LogEventLevel> Map = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Trace"] = LogEventLevel.Verbose,
            ["Debug"] = LogEventLevel.Debug,
            ["Information"] = LogEventLevel.Information,
            ["Warning"] = LogEventLevel.Warning,
            ["Error"] = LogEventLevel.Error,
            ["Critical"] = LogEventLevel.Fatal,
            ["None"] = LogEventLevel.Fatal
        };

        public static bool TryParse(string? value, out LogEventLevel level)
        {
            if (value != null && Map.TryGetValue(value, out level))
            {
                return true;
            }

            level = LogEventLevel.Information;
            return false;
        }
    }
}