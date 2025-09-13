using DevPod.Provider.ACI.Models;
using Microsoft.Extensions.Logging;

namespace DevPod.Provider.ACI.Services;

public class ProviderOptionsService(ILogger<ProviderOptionsService> logger) : IProviderOptionsService
{
    private ProviderOptions? _cachedOptions;

    public ProviderOptions GetOptions()
    {
        if (_cachedOptions == null)
        {
            _cachedOptions = ProviderOptions.FromEnvironment();
            logger.LogDebug("Loaded provider options from environment");
        }

        return _cachedOptions;
    }

    public bool ValidateOptions(ProviderOptions options, out List<string> errors)
    {
        errors = [];

        // Validate required Azure settings
        if (string.IsNullOrEmpty(options.AzureSubscriptionId))
        {
            errors.Add("AZURE_SUBSCRIPTION_ID is required");
        }

        if (string.IsNullOrEmpty(options.AzureResourceGroup))
        {
            errors.Add("AZURE_RESOURCE_GROUP is required");
        }

        if (string.IsNullOrEmpty(options.AzureRegion))
        {
            errors.Add("AZURE_REGION is required");
        }

        // Validate resource constraints
        if (options.AciCpuCores is < 0.25 or > 4)
        {
            errors.Add("ACI_CPU_CORES must be between 0.25 and 4");
        }

        if (options.AciMemoryGb is < 0.5 or > 16)
        {
            errors.Add("ACI_MEMORY_GB must be between 0.5 and 16");
        }

        // Validate network configuration if provided
        if (!string.IsNullOrEmpty(options.AciVnetName) && string.IsNullOrEmpty(options.AciSubnetName))
        {
            errors.Add("ACI_SUBNET_NAME is required when ACI_VNET_NAME is specified");
        }

        // Validate registry configuration if provided
        if (!string.IsNullOrEmpty(options.AcrServer))
        {
            var mode = (options.AcrAuthMode ?? "ManagedIdentity").Trim();
            switch (mode.ToLowerInvariant())
            {
                case "managedidentity":
                    // No username/password required. If user-assigned identity is provided, assume valid.
                    break;
                case "keyvault":
                    if (string.IsNullOrEmpty(options.KeyVaultUri))
                    {
                        errors.Add("KEYVAULT_URI is required when ACR_AUTH_MODE is KeyVault");
                    }
                    if (string.IsNullOrEmpty(options.AcrUsernameSecretName) || string.IsNullOrEmpty(options.AcrPasswordSecretName))
                    {
                        errors.Add("ACR_USERNAME_SECRET_NAME and ACR_PASSWORD_SECRET_NAME are required when ACR_AUTH_MODE is KeyVault");
                    }
                    break;
                case "usernamepassword":
                default:
                    if (string.IsNullOrEmpty(options.AcrUsername) || string.IsNullOrEmpty(options.AcrPassword))
                    {
                        errors.Add("ACR_USERNAME and ACR_PASSWORD are required when ACR_AUTH_MODE is UsernamePassword");
                    }
                    break;
            }
        }

        // Validate storage configuration if provided
        if (!string.IsNullOrEmpty(options.AciStorageAccountName) && string.IsNullOrEmpty(options.AciStorageAccountKey))
        {
            errors.Add("ACI_STORAGE_ACCOUNT_KEY is required when ACI_STORAGE_ACCOUNT_NAME is specified");
        }

        return errors.Count == 0;
    }
}
