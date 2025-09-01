using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;

namespace DevPod.Provider.ACI.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly ILogger<AuthenticationService> _logger;
    private readonly IProviderOptionsService _optionsService;
    private ArmClient? _armClient;
    private TokenCredential? _credential;

    public AuthenticationService(
        ILogger<AuthenticationService> logger,
        IProviderOptionsService optionsService)
    {
        _logger = logger;
        _optionsService = optionsService;
    }

    public async Task<ArmClient> GetArmClientAsync()
    {
        if (_armClient == null)
        {
            var credential = GetCredential();
            _armClient = new ArmClient(credential);
            _logger.LogDebug("Created Azure ARM client");
        }
        return _armClient;
    }

    public TokenCredential GetCredential()
    {
        if (_credential == null)
        {
            var options = _optionsService.GetOptions();

            // Try service principal authentication first
            if (!string.IsNullOrEmpty(options.AzureClientId) &&
                !string.IsNullOrEmpty(options.AzureClientSecret) &&
                !string.IsNullOrEmpty(options.AzureTenantId))
            {
                _logger.LogInformation("Using service principal authentication");
                _credential = new ClientSecretCredential(
                    options.AzureTenantId,
                    options.AzureClientId,
                    options.AzureClientSecret);
            }
            else
            {
                // Fall back to default Azure credential chain
                _logger.LogInformation("Using default Azure credential chain");
                _credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    ExcludeInteractiveBrowserCredential = true,
                    ExcludeVisualStudioCredential = true,
                    ExcludeVisualStudioCodeCredential = true
                });
            }
        }
        return _credential;
    }
}
