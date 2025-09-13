using DevPod.Provider.ACI.Models;
using DevPod.Provider.ACI.Services;
using Microsoft.Extensions.Logging;

namespace DevPod.Provider.ACI.Commands;

public class CreateCommand(
    ILogger<CreateCommand> logger,
    IProviderOptionsService optionsService,
    IAciService aciService,
    ISecretService secretService)
{
    public async Task<int> ExecuteAsync()
    {
        logger.LogInformation("Creating Azure Container Instance");

        try
        {
            var options = optionsService.GetOptions();

            // Get workspace info from environment
            var workspaceImage = Environment.GetEnvironmentVariable("WORKSPACE_IMAGE") ?? "mcr.microsoft.com/devcontainers/base:ubuntu";
            var workspaceSource = Environment.GetEnvironmentVariable("WORKSPACE_SOURCE");

            var definition = new ContainerGroupDefinition
            {
                Name = options.GetContainerGroupName(),
                ResourceGroup = options.AzureResourceGroup,
                Location = options.AzureRegion,
                Image = workspaceImage,
                CpuCores = options.AciCpuCores,
                MemoryGb = options.AciMemoryGb,
                GpuCount = options.AciGpuCount,
                RestartPolicy = options.AciRestartPolicy,
                DnsNameLabel = options.AciDnsLabel,
            };

            // Add environment variables
            definition.EnvironmentVariables["DEVPOD_AGENT_PATH"] = options.AgentPath;
            definition.EnvironmentVariables["WORKSPACE_SOURCE"] = workspaceSource ?? string.Empty;

            if (options.InjectGitCredentials)
            {
                var gitUsername = Environment.GetEnvironmentVariable("GIT_USERNAME");
                var gitToken = Environment.GetEnvironmentVariable("GIT_TOKEN");
                if (!string.IsNullOrEmpty(gitUsername) && !string.IsNullOrEmpty(gitToken))
                {
                    definition.EnvironmentVariables["GIT_USERNAME"] = gitUsername;
                    definition.EnvironmentVariables["GIT_TOKEN"] = gitToken;
                }
            }

            // Configure registry authentication if ACR is specified
            if (!string.IsNullOrEmpty(options.AcrServer))
            {
                var mode = (options.AcrAuthMode ?? "ManagedIdentity").Trim();
                if (mode.Equals("ManagedIdentity", StringComparison.OrdinalIgnoreCase))
                {
                    // Secretless pull via ACI managed identity. Ensure identity is configured in ACI service.
                    logger.LogInformation("Using Managed Identity for ACR authentication");
                }
                else if (mode.Equals("KeyVault", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation("Fetching ACR credentials from Key Vault");
                    var username = await secretService.GetAsync(options.AcrUsernameSecretName!);
                    var password = await secretService.GetAsync(options.AcrPasswordSecretName!);

                    if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                    {
                        throw new InvalidOperationException("Failed to retrieve ACR credentials from Key Vault");
                    }

                    definition.RegistryCredentials = new ContainerRegistryCredentials
                    {
                        Server = options.AcrServer,
                        Username = username,
                        Password = password,
                    };
                }
                else
                {
                    // Username/Password mode (env-based or provided via options)
                    definition.RegistryCredentials = new ContainerRegistryCredentials
                    {
                        Server = options.AcrServer,
                        Username = options.AcrUsername!,
                        Password = options.AcrPassword!,
                    };
                }
            }

            // Configure storage if provided
            if (!string.IsNullOrEmpty(options.AciStorageAccountName))
            {
                definition.FileShareVolume = new AzureFileShareVolume
                {
                    Name = "workspace",
                    ShareName = options.AciFileShareName!,
                    StorageAccountName = options.AciStorageAccountName,
                    StorageAccountKey = options.AciStorageAccountKey!,
                    MountPath = "/workspace",
                };
            }

            // Configure network if provided
            if (!string.IsNullOrEmpty(options.AciVnetName))
            {
                // Note: You'd need to resolve the subnet ID from vnet/subnet names
                // This is simplified for brevity
                definition.NetworkProfile = new NetworkProfile
                {
                    VnetName = options.AciVnetName,
                    SubnetName = options.AciSubnetName!,
                };
            }

            // Add SSH port for DevPod connection
            definition.Ports.Add(22);

            // Create the container group
            var status = await aciService.CreateContainerGroupAsync(definition);

            logger.LogInformation("Container group created: {ContainerGroupName}", status.Name);
            Console.WriteLine($"###START_CONTAINER###");
            Console.WriteLine($"Name: {status.Name}");
            Console.WriteLine($"Status: {status.State}");
            if (!string.IsNullOrEmpty(status.Fqdn))
            {
                Console.WriteLine($"FQDN: {status.Fqdn}");
            }

            if (!string.IsNullOrEmpty(status.IpAddress))
            {
                Console.WriteLine($"IP: {status.IpAddress}");
            }

            Console.WriteLine($"###END_CONTAINER###");

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create container group");
            Console.Error.WriteLine($"Create failed: {ex.Message}");
            return 1;
        }
    }
}
