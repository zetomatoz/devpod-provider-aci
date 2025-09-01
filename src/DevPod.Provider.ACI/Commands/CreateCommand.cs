namespace DevPod.Provider.ACI.Commands;

using DevPod.Provider.ACI.Models;
using DevPod.Provider.ACI.Services;
using Microsoft.Extensions.Logging;

public class CreateCommand
{
    private readonly ILogger<CreateCommand> _logger;
    private readonly IProviderOptionsService _optionsService;
    private readonly IAciService _aciService;

    public CreateCommand(
        ILogger<CreateCommand> logger,
        IProviderOptionsService optionsService,
        IAciService aciService)
    {
        _logger = logger;
        _optionsService = optionsService;
        _aciService = aciService;
    }

    public async Task<int> ExecuteAsync()
    {
        _logger.LogInformation("Creating Azure Container Instance");

        try
        {
            var options = _optionsService.GetOptions();

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
                DnsNameLabel = options.AciDnsLabel
            };

            // Add environment variables
            definition.EnvironmentVariables["DEVPOD_AGENT_PATH"] = options.AgentPath;
            definition.EnvironmentVariables["WORKSPACE_SOURCE"] = workspaceSource ?? "";

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

            // Configure registry credentials if provided
            if (!string.IsNullOrEmpty(options.AcrServer))
            {
                definition.RegistryCredentials = new ContainerRegistryCredentials
                {
                    Server = options.AcrServer,
                    Username = options.AcrUsername!,
                    Password = options.AcrPassword! //todo ensure it follows security best practices!
                };
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
                    MountPath = "/workspace"
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
                    SubnetName = options.AciSubnetName!
                };
            }

            // Add SSH port for DevPod connection
            definition.Ports.Add(22);

            // Create the container group
            var status = await _aciService.CreateContainerGroupAsync(definition);

            _logger.LogInformation($"Container group created: {status.Name}");
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
            _logger.LogError(ex, "Failed to create container group");
            Console.Error.WriteLine($"Create failed: {ex.Message}),", ex);
            return 1;
        }
    }
}