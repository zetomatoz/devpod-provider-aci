using DevPod.Provider.ACI.Services;
using Microsoft.Extensions.Logging;

namespace DevPod.Provider.ACI.Commands;

public class InitCommand(
    ILogger<InitCommand> logger,
    IProviderOptionsService optionsService,
    IAuthenticationService authService)
{
    public async Task<int> ExecuteAsync()
    {
        logger.LogInformation("Initializing Azure Container Instances provider");

        try
        {
            // Get and validate options
            var options = optionsService.GetOptions();

            if (!optionsService.ValidateOptions(options, out var errors))
            {
                foreach (var error in errors)
                {
                    Console.Error.WriteLine($"Error: {error}");
                }

                return 1;
            }

            // Test Azure connection
            logger.LogInformation("Testing Azure connection...");
            var armClient = await authService.GetArmClientAsync();
            var subscription = await armClient.GetDefaultSubscriptionAsync();

            logger.LogInformation("Successfully connected to Azure subscription: {DisplayName}", subscription.Data.DisplayName);
            Console.WriteLine($"Azure Container Instances provider initialized successfully");
            Console.WriteLine($"Subscription: {subscription.Data.DisplayName}");
            Console.WriteLine($"Resource Group: {options.AzureResourceGroup}");
            Console.WriteLine($"Region: {options.AzureRegion}");

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize provider");
            Console.Error.WriteLine($"Initialization failed: {ex.Message}");
            return 1;
        }
    }
}
