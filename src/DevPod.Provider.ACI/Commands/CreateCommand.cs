using Azure;
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
            if (!optionsService.ValidateOptions(options, out var optionErrors))
            {
                foreach (var error in optionErrors)
                {
                    Console.Error.WriteLine($"Error: {error}");
                }

                return 1;
            }

            // Get workspace info from environment
            var workspaceImage = Environment.GetEnvironmentVariable("WORKSPACE_IMAGE");
            var workspaceSource = Environment.GetEnvironmentVariable("WORKSPACE_SOURCE");
            var createErrors = ValidateCreateRequest(workspaceImage, workspaceSource);
            if (createErrors.Count > 0)
            {
                foreach (var error in createErrors)
                {
                    Console.Error.WriteLine($"Error: {error}");
                }

                return 1;
            }

            var definition = new ContainerGroupDefinition
            {
                Name = options.GetContainerGroupName(),
                ResourceGroup = options.AzureResourceGroup,
                Location = options.AzureRegion,
                Image = workspaceImage!,
                CpuCores = options.AciCpuCores,
                MemoryGb = options.AciMemoryGb,
                GpuCount = options.AciGpuCount,
                RestartPolicy = options.AciRestartPolicy,
                DnsNameLabel = options.AciDnsLabel,
            };

            // Add environment variables
            definition.EnvironmentVariables["DEVPOD_AGENT_PATH"] = options.AgentPath;
            definition.EnvironmentVariables["AZURE_RESOURCE_GROUP"] = options.AzureResourceGroup;
            definition.EnvironmentVariables["AZURE_REGION"] = options.AzureRegion;
            definition.EnvironmentVariables["ACI_CONTAINER_GROUP_NAME"] = definition.Name;
            AddOptionalEnvironmentVariable(definition, "MACHINE_ID", options.MachineId);
            AddOptionalEnvironmentVariable(definition, "WORKSPACE_ID", options.WorkspaceId);
            AddOptionalEnvironmentVariable(definition, "WORKSPACE_UID", options.WorkspaceUid);

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
            Console.Error.WriteLine($"Create failed: {DescribeCreateFailure(ex)}");
            return 1;
        }
    }

    private static List<string> ValidateCreateRequest(string? workspaceImage, string? workspaceSource)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(workspaceImage))
        {
            errors.Add("WORKSPACE_IMAGE is required. This provider currently supports image-based workspaces only.");
        }

        if (LooksLikeUnsupportedWorkspaceSource(workspaceSource))
        {
            errors.Add($"WORKSPACE_SOURCE '{workspaceSource}' is not supported. This provider currently supports published image workspaces only.");
        }

        return errors;
    }

    private static bool LooksLikeUnsupportedWorkspaceSource(string? workspaceSource)
    {
        if (string.IsNullOrWhiteSpace(workspaceSource))
        {
            return false;
        }

        var trimmed = workspaceSource.Trim();
        return trimmed.StartsWith("./", StringComparison.Ordinal) ||
               trimmed.StartsWith("../", StringComparison.Ordinal) ||
               trimmed.StartsWith("~/", StringComparison.Ordinal) ||
               Path.IsPathRooted(trimmed) ||
               trimmed.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("git@", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
               trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddOptionalEnvironmentVariable(ContainerGroupDefinition definition, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            definition.EnvironmentVariables[name] = value;
        }
    }

    private static string DescribeCreateFailure(Exception ex)
    {
        if (ex is RequestFailedException requestFailedException &&
            requestFailedException.ErrorCode == "MissingSubscriptionRegistration" &&
            requestFailedException.Message.Contains("Microsoft.ContainerInstance", StringComparison.OrdinalIgnoreCase))
        {
            return "The Azure subscription is not registered for Microsoft.ContainerInstance. Run 'az provider register --namespace Microsoft.ContainerInstance' and wait for registration to complete, then retry.";
        }

        if (ex is RequestFailedException imageMismatchException &&
            imageMismatchException.ErrorCode == "ImageOsTypeNotMatchContainerGroup" &&
            imageMismatchException.Message.Contains("doesn't support specified OS 'Linux'", StringComparison.OrdinalIgnoreCase))
        {
            return "The workspace image is not published as a Linux ACI-compatible image. Rebuild and republish it as 'linux/amd64', then retry.";
        }

        return ex.Message;
    }
}
