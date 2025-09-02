using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace DevPod.Provider.ACI.Infrastructure;

// todo Adjust looging using Serilog
public static class Logger
{
    public static void ConfigureLogging(ILoggingBuilder builder)
    {
        builder.ClearProviders();

        builder.AddSimpleConsole(options =>
        {
            options.ColorBehavior = LoggerColorBehavior.Enabled;
            options.IncludeScopes = true;
        });

        // Set log level based on debug mode
        var debugMode = Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.DevPodDebug);
        if (!string.IsNullOrEmpty(debugMode) && debugMode.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddFilter("Azure", LogLevel.Debug);
            builder.AddFilter("System", LogLevel.Debug);
            builder.AddFilter("Microsoft", LogLevel.Debug);
        }
        else
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddFilter("Azure", LogLevel.Warning);
            builder.AddFilter("System", LogLevel.Warning);
            builder.AddFilter("Microsoft", LogLevel.Warning);
        }
    }

    public static void LogToStderr(string message)
    {
        Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {message}");
    }
}
