using DevPod.Provider.ACI.Services;
using Microsoft.Extensions.Logging;

namespace DevPod.Provider.ACI.Commands;

public class InitCommand
{
    private readonly ILogger<InitCommand> _logger;
    private readonly IProviderOptionsService _optionsService;
    private readonly IAuthenticationService _authService;

    public InitCommand(
        ILogger<InitCommand> logger,
        IProviderOptionsService optionsService,
        IAuthenticationService authService)
    {
        _logger = logger;
        _optionsService = optionsService;
        _authService = authService;
    }

    public async Task<int> ExecuteAsync()
    {
        _logger.LogInformation("Initializing Azure Container Instances provider");

        try
        {
            // Get and validate options
            var options = _optionsService.GetOptions();

            if (!_optionsService.ValidateOptions(options, out var errors))
            {
                foreach (var error in errors)
                {
                    Console.Error.WriteLine($"Error: {error}");
                }
                return 1;
            }

            // Test Azure connection
            _logger.LogInformation("Testing Azure connection...");
            var armClient = await _authService.GetArmClientAsync();
            var subscription = await armClient.GetDefaultSubscriptionAsync();

            _logger.LogInformation($"Successfully connected to Azure subscription: {subscription.Data.DisplayName}");
            Console.WriteLine($"Azure Container Instances provider initialized successfully");
            Console.WriteLine($"Subscription: {subscription.Data.DisplayName}");
            Console.WriteLine($"Resource Group: {options.AzureResourceGroup}");
            Console.WriteLine($"Region: {options.AzureRegion}");

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize provider");
            Console.Error.WriteLine($"Initialization failed: {ex.Message}");
            return 1;
        }
    }
}
