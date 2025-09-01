using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DevPod.Provider.ACI.Commands;
using DevPod.Provider.ACI.Infrastructure;
using DevPod.Provider.ACI.Services;

namespace DevPod.Provider.ACI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Setup DI container
        var services = new ServiceCollection();
        ConfigureServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Setup command router
        var router = new CommandRouter(serviceProvider);
        
        try
        {
            // If no args, show help
            if (args.Length == 0)
            {
                ShowHelp();
                return 0;
            }

            // Route to appropriate command
            return await router.RouteAsync(args);
        }
        catch (Exception ex)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Unhandled exception occurred");
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        finally
        {
            if (serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Add configuration
        services.AddSingleton<IConfiguration>(configuration);

        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
            
            // Check for debug mode
            var debugMode = Environment.GetEnvironmentVariable("DEVPOD_DEBUG");
            if (!string.IsNullOrEmpty(debugMode) && debugMode.ToLower() == "true")
            {
                builder.SetMinimumLevel(LogLevel.Debug);
            }
        });

        // Add services
        services.AddSingleton<IProviderOptionsService, ProviderOptionsService>();
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<IAciService, AciService>();

        // Add commands
        services.AddTransient<InitCommand>();
        services.AddTransient<CreateCommand>();
        services.AddTransient<DeleteCommand>();
        services.AddTransient<StartCommand>();
        services.AddTransient<StopCommand>();
        services.AddTransient<StatusCommand>();
        services.AddTransient<CommandCommand>();
    }

    private static void ShowHelp()
    {
        Console.WriteLine("DevPod Provider for Azure Container Instances");
        Console.WriteLine("Version: 0.1.0");
        Console.WriteLine();
        Console.WriteLine("Usage: devpod-provider-aci <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  init      - Initialize and validate provider configuration");
        Console.WriteLine("  create    - Create a new container instance");
        Console.WriteLine("  delete    - Delete an existing container instance");
        Console.WriteLine("  start     - Start a stopped container instance");
        Console.WriteLine("  stop      - Stop a running container instance");
        Console.WriteLine("  status    - Get the status of a container instance");
        Console.WriteLine("  command   - Execute a command in the container");
        Console.WriteLine();
        Console.WriteLine("Environment Variables:");
        Console.WriteLine("  MACHINE_ID                - The DevPod machine ID");
        Console.WriteLine("  MACHINE_FOLDER           - The DevPod machine folder");
        Console.WriteLine("  AZURE_SUBSCRIPTION_ID    - Azure subscription ID");
        Console.WriteLine("  AZURE_RESOURCE_GROUP     - Azure resource group name");
        Console.WriteLine("  AZURE_REGION             - Azure region");
        Console.WriteLine("  Plus all options defined in provider.yaml");
    }
}