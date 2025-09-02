using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;

namespace DevPod.Provider.ACI.Services;

public class AuthenticationService(
    ILogger<AuthenticationService> logger,
    IProviderOptionsService optionsService) : IAuthenticationService
{
    private ArmClient? _armClient;
    private TokenCredential? _credential;

    public async Task<ArmClient> GetArmClientAsync()
    {
        if (_armClient == null)
        {
            var credential = GetCredential();
            _armClient = new ArmClient(credential);
            logger.LogDebug("Created Azure ARM client");
        }

        return _armClient;
    }

    public TokenCredential GetCredential()
    {
        if (_credential == null)
        {
            var options = optionsService.GetOptions();

            // Try service principal authentication first
            if (!string.IsNullOrEmpty(options.AzureClientId) &&
                !string.IsNullOrEmpty(options.AzureClientSecret) &&
                !string.IsNullOrEmpty(options.AzureTenantId))
            {
                logger.LogInformation("Using service principal authentication");
                _credential = new ClientSecretCredential(
                    options.AzureTenantId,
                    options.AzureClientId,
                    options.AzureClientSecret);
            }
            else
            {
                // Fall back to default Azure credential chain
                logger.LogInformation("Using default Azure credential chain");
                _credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    ExcludeInteractiveBrowserCredential = true,
                    ExcludeVisualStudioCredential = true,
                    ExcludeVisualStudioCodeCredential = true,
                });
            }
        }

        return _credential;
    }
}
