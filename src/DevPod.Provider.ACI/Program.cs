using DevPod.Provider.ACI.Commands;
using DevPod.Provider.ACI.Infrastructure;
using DevPod.Provider.ACI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

// Setup DI container
var services = new ServiceCollection();
ConfigureServices(services, configuration);
var serviceProvider = services.BuildServiceProvider();

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
    await serviceProvider.DisposeAsync();
}

void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    services
        .AddSingleton(configuration)
        .AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);

            // Check for debug mode
            var debugMode = Environment.GetEnvironmentVariable("DEVPOD_DEBUG");
            if (!string.IsNullOrEmpty(debugMode) && debugMode.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                builder.SetMinimumLevel(LogLevel.Debug);
            }
        })
        .AddSingleton<IProviderOptionsService, ProviderOptionsService>()
        .AddSingleton<IAuthenticationService, AuthenticationService>()
        .AddSingleton<IAciService, AciService>()
        .AddTransient<InitCommand>()
        .AddTransient<CreateCommand>()
        .AddTransient<DeleteCommand>()
        .AddTransient<StartCommand>()
        .AddTransient<StopCommand>()
        .AddTransient<StatusCommand>()
        .AddTransient<CommandCommand>();
}

void ShowHelp()
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
