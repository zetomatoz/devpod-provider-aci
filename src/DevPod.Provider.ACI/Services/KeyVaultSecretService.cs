using Azure;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;

namespace DevPod.Provider.ACI.Services;

public class KeyVaultSecretService(
    ILogger<KeyVaultSecretService> logger,
    IAuthenticationService authService,
    IProviderOptionsService optionsService) : ISecretService
{
    private readonly ILogger<KeyVaultSecretService> _logger = logger;
    private readonly IAuthenticationService _authService = authService;
    private readonly IProviderOptionsService _optionsService = optionsService;
    private SecretClient? _client;

    public async Task<string?> GetAsync(string secretName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretName))
        {
            return null;
        }

        var client = await GetClientAsync(cancellationToken);
        if (client == null)
        {
            _logger.LogDebug("Key Vault client not configured; skipping secret fetch for {SecretName}", secretName);
            return null;
        }

        try
        {
            var response = await client.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            return response.Value.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Secret not found in Key Vault: {SecretName}", secretName);
            return null;
        }
    }

    private async Task<SecretClient?> GetClientAsync(CancellationToken cancellationToken)
    {
        if (_client != null)
        {
            return _client;
        }

        var options = _optionsService.GetOptions();
        if (string.IsNullOrWhiteSpace(options.KeyVaultUri))
        {
            return null;
        }

        var credential = _authService.GetCredential();
        _client = new SecretClient(new Uri(options.KeyVaultUri), credential);
        await Task.CompletedTask;
        _logger.LogDebug("Initialized Key Vault secret client for {Vault}", options.KeyVaultUri);
        return _client;
    }
}

