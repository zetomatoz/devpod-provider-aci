namespace DevPod.Provider.ACI.Infrastructure;

using Microsoft.Extensions.Logging;

//todo Adjust looging using Serilog
public static class Logger
{
    public static void ConfigureLogging(ILoggingBuilder builder)
    {
        builder.ClearProviders();
        
        // Add console logger with custom format
        builder.AddConsole(options =>
        {
            options.DisableColors = false;
            options.IncludeScopes = true;
        });

        // Set log level based on debug mode
        var debugMode = Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.DevPodDebug);
        if (!string.IsNullOrEmpty(debugMode) && debugMode.ToLower() == "true")
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