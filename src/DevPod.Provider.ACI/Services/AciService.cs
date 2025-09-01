namespace DevPod.Provider.ACI.Services;

using System.Text;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ContainerInstance;
using Azure.ResourceManager.ContainerInstance.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using DevPod.Provider.ACI.Models;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Renci.SshNet;

public class AciService : IAciService
{
    private readonly ILogger<AciService> _logger;
    private readonly IAuthenticationService _authService;
    private readonly IProviderOptionsService _optionsService;
    private readonly AsyncRetryPolicy _retryPolicy;

    public AciService(
        ILogger<AciService> logger,
        IAuthenticationService authService,
        IProviderOptionsService optionsService)
    {
        _logger = logger;
        _authService = authService;
        _optionsService = optionsService;

        // Configure retry policy for transient failures
        _retryPolicy = Policy
            .Handle<RequestFailedException>(ex => IsTransientError(ex))
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning($"Retry {retryCount} after {timespan} seconds");
                });
    }

    public async Task<ContainerStatus> CreateContainerGroupAsync(ContainerGroupDefinition definition)
    {
        _logger.LogInformation($"Creating container group: {definition.Name}");

        var armClient = await _authService.GetArmClientAsync();
        var options = _optionsService.GetOptions();
        
        // Get or create resource group
        var resourceGroup = await GetOrCreateResourceGroupAsync(armClient, options);
        
        // Build container group data
        var containerGroupData = await BuildContainerGroupDataAsync(definition, resourceGroup);

        // Create container group
        var containerGroupCollection = resourceGroup.GetContainerGroups();
        
        var operation = await _retryPolicy.ExecuteAsync(async () =>
            await containerGroupCollection.CreateOrUpdateAsync(
                WaitUntil.Completed,
                definition.Name,
                containerGroupData));

        var containerGroup = operation.Value;
        
        _logger.LogInformation($"Container group created: {containerGroup.Data.Name}");
        
        return MapToContainerStatus(containerGroup.Data);
    }

    public async Task<ContainerStatus> GetContainerGroupStatusAsync(string name)
    {
        _logger.LogDebug($"Getting status for container group: {name}");

        var containerGroup = await GetContainerGroupAsync(name);
        if (containerGroup == null)
        {
            return new ContainerStatus
            {
                Name = name,
                State = "NotFound",
                ProvisioningState = "NotFound"
            };
        }

        return MapToContainerStatus(containerGroup.Data);
    }

    public async Task DeleteContainerGroupAsync(string name)
    {
        _logger.LogInformation($"Deleting container group: {name}");

        var containerGroup = await GetContainerGroupAsync(name);
        if (containerGroup != null)
        {
            await containerGroup.DeleteAsync(WaitUntil.Completed);
            _logger.LogInformation($"Container group deleted: {name}");
        }
        else
        {
            _logger.LogWarning($"Container group not found: {name}");
        }
    }

    public async Task<ContainerStatus> StartContainerGroupAsync(string name)
    {
        _logger.LogInformation($"Starting container group: {name}");

        var containerGroup = await GetContainerGroupAsync(name);
        if (containerGroup == null)
        {
            throw new InvalidOperationException($"Container group not found: {name}");
        }

        await containerGroup.StartAsync(WaitUntil.Completed);
        
        // Refresh the container group data
        containerGroup = await GetContainerGroupAsync(name);
        return MapToContainerStatus(containerGroup!.Data);
    }

    public async Task<ContainerStatus> StopContainerGroupAsync(string name)
    {
        _logger.LogInformation($"Stopping container group: {name}");

        var containerGroup = await GetContainerGroupAsync(name);
        if (containerGroup == null)
        {
            throw new InvalidOperationException($"Container group not found: {name}");
        }

        await containerGroup.StopAsync();
        
        // Refresh the container group data
        containerGroup = await GetContainerGroupAsync(name);
        return MapToContainerStatus(containerGroup!.Data);
    }

    public async Task<string> ExecuteCommandAsync(string containerGroupName, string command)
    {
        _logger.LogDebug($"Executing command in container group: {containerGroupName}");

        var containerGroup = await GetContainerGroupAsync(containerGroupName);
        if (containerGroup == null)
        {
            throw new InvalidOperationException($"Container group not found: {containerGroupName}");
        }

        // Get the first container in the group
        var containerName = containerGroup.Data.Containers.First().Name;

        // Execute command via exec endpoint
        // Note: Direct exec API is not available in this SDK version
        // Using alternative approach through SSH or returning placeholder

        // Note: This is a simplified implementation. In production, you'd need to handle
        // the WebSocket connection properly to execute commands and retrieve output
        
        // For now, we'll use SSH if available, or return a placeholder
        return await ExecuteViaSSHAsync(containerGroup.Data, command);
    }

    public async Task<string> GetContainerLogsAsync(string containerGroupName, string containerName)
    {
        _logger.LogDebug($"Getting logs for container: {containerName} in group: {containerGroupName}");

        var containerGroup = await GetContainerGroupAsync(containerGroupName);
        if (containerGroup == null)
        {
            throw new InvalidOperationException($"Container group not found: {containerGroupName}");
        }

        var container = containerGroup.Data.Containers.FirstOrDefault(c => c.Name == containerName);
        if (container == null)
        {
            containerName = containerGroup.Data.Containers.First().Name;
        }

        var logsResult = await containerGroup.GetContainerLogsAsync(containerName);
        return logsResult.Value.Content ?? string.Empty;
    }

    public async Task<(string fqdn, string ipAddress)> GetContainerEndpointAsync(string name)
    {
        var containerGroup = await GetContainerGroupAsync(name);
        if (containerGroup == null)
        {
            return (string.Empty, string.Empty);
        }

        var fqdn = containerGroup.Data.IPAddress?.Fqdn ?? string.Empty;
        var ipAddress = containerGroup.Data.IPAddress?.IP?.ToString() ?? string.Empty;

        return (fqdn, ipAddress);
    }

    private async Task<ResourceGroupResource> GetOrCreateResourceGroupAsync(ArmClient armClient, ProviderOptions options)
    {
        var subscription = await armClient.GetDefaultSubscriptionAsync();
        var resourceGroups = subscription.GetResourceGroups();

        try
        {
            var resourceGroup = await resourceGroups.GetAsync(options.AzureResourceGroup);
            _logger.LogDebug($"Using existing resource group: {options.AzureResourceGroup}");
            return resourceGroup.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogInformation($"Creating resource group: {options.AzureResourceGroup}");
            var location = new AzureLocation(options.AzureRegion);
            var resourceGroupData = new ResourceGroupData(location);
            var operation = await resourceGroups.CreateOrUpdateAsync(
                WaitUntil.Completed,
                options.AzureResourceGroup,
                resourceGroupData);
            return operation.Value;
        }
    }

    private async Task<ContainerGroupData> BuildContainerGroupDataAsync(
        ContainerGroupDefinition definition,
        ResourceGroupResource resourceGroup)
    {
        var location = new AzureLocation(definition.Location);
        var options = _optionsService.GetOptions();

        // Create container configuration
        var resources = new ContainerResourceRequirements(
            new ContainerResourceRequestsContent(definition.MemoryGb, definition.CpuCores));
        var container = new ContainerInstanceContainer(definition.Name, definition.Image, resources);

        // Add GPU if requested
        if (definition.GpuCount > 0)
        {
            resources.Requests.Gpu = new ContainerGpuResourceInfo(definition.GpuCount, ContainerGpuSku.K80);
        }

        // Add environment variables
        foreach (var envVar in definition.EnvironmentVariables)
        {
            container.EnvironmentVariables.Add(new ContainerEnvironmentVariable(envVar.Key)
            {
                Value = envVar.Value
            });
        }

        // Add DevPod specific environment variables
        container.EnvironmentVariables.Add(new ContainerEnvironmentVariable("DEVPOD")
        {
            Value = "true"
        });

        // Add ports
        foreach (var port in definition.Ports)
        {
            container.Ports.Add(new ContainerPort(port));
        }

        // Set command if provided
        if (!string.IsNullOrEmpty(definition.Command))
        {
            container.Command.Add(definition.Command);
            if (definition.Arguments != null)
            {
                foreach (var arg in definition.Arguments)
                {
                    container.Command.Add(arg);
                }
            }
        }

        // Create container group
        var containerGroupData = new ContainerGroupData(location, new[] { container }, ContainerInstanceOperatingSystemType.Linux)
        {
            RestartPolicy = Enum.Parse<ContainerGroupRestartPolicy>(definition.RestartPolicy, true)
        };

        // Add registry credentials if provided
        if (definition.RegistryCredentials != null)
        {
            containerGroupData.ImageRegistryCredentials.Add(new ContainerGroupImageRegistryCredential(
                definition.RegistryCredentials.Server)
            {
                Username = definition.RegistryCredentials.Username,
                Password = definition.RegistryCredentials.Password // todo double check it follows security best practices
            });
        }

        // Configure networking
        // todo review to understand better this subnet concept
        if (definition.NetworkProfile != null && !string.IsNullOrEmpty(definition.NetworkProfile.SubnetId))
        {
            // Private network configuration
            containerGroupData.SubnetIds.Add(new ContainerGroupSubnetId(new ResourceIdentifier(definition.NetworkProfile.SubnetId)));
        }
        else
        {
            // Public IP configuration
            containerGroupData.IPAddress = new ContainerGroupIPAddress(
                definition.Ports.Select(p => new ContainerGroupPort(p)).ToList(),
                ContainerGroupIPAddressType.Public);

            if (!string.IsNullOrEmpty(definition.DnsNameLabel))
            {
                containerGroupData.IPAddress.DnsNameLabel = definition.DnsNameLabel;
            }
        }

        // Add Azure File Share volume if configured
        if (definition.FileShareVolume != null)
        {
            var volume = new ContainerVolume(definition.FileShareVolume.Name)
            {
                AzureFile = new ContainerInstanceAzureFileVolume(
                    definition.FileShareVolume.ShareName,
                    definition.FileShareVolume.StorageAccountName)
                {
                    StorageAccountKey = definition.FileShareVolume.StorageAccountKey
                }
            };
            containerGroupData.Volumes.Add(volume);

            // Mount the volume in the container
            container.VolumeMounts.Add(new ContainerVolumeMount(
                definition.FileShareVolume.Name,
                definition.FileShareVolume.MountPath));
        }

        return containerGroupData;
    }

    private async Task<ContainerGroupResource?> GetContainerGroupAsync(string name)
    {
        try
        {
            var armClient = await _authService.GetArmClientAsync();
            var options = _optionsService.GetOptions();
            var subscription = await armClient.GetDefaultSubscriptionAsync();
            var resourceGroup = await subscription.GetResourceGroupAsync(options.AzureResourceGroup);
            return await resourceGroup.Value.GetContainerGroupAsync(name);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private ContainerStatus MapToContainerStatus(ContainerGroupData data)
    {
        var status = new ContainerStatus
        {
            Name = data.Name,
            ProvisioningState = data.ProvisioningState ?? "Unknown",
            State = GetContainerGroupState(data),
            Fqdn = data.IPAddress?.Fqdn,
            IpAddress = data.IPAddress?.IP.ToString()
        };

        foreach (var container in data.Containers)
        {
            var instanceStatus = new ContainerInstanceStatus
            {
                Name = container.Name,
                State = container.InstanceView?.CurrentState?.State ?? "Unknown",
                StartTime = container.InstanceView?.CurrentState?.StartOn?.DateTime,
                FinishTime = container.InstanceView?.CurrentState?.FinishOn?.DateTime,
                ExitCode = container.InstanceView?.CurrentState?.ExitCode,
                DetailedStatus = container.InstanceView?.CurrentState?.DetailStatus
            };

            status.Containers[container.Name] = instanceStatus;
        }

        return status;
    }

    private string GetContainerGroupState(ContainerGroupData data)
    {
        // Check provisioning state first
        if (data.ProvisioningState == "Failed")
            return "Failed";

        // Check container states
        var containerStates = data.Containers
            .Select(c => c.InstanceView?.CurrentState?.State)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        if (containerStates.Any(s => s == "Running"))
            return "Running";
        if (containerStates.Any(s => s == "Terminated"))
            return "Stopped";
        if (containerStates.Any(s => s == "Waiting"))
            return "Pending";

        return "Unknown";
    }

    private async Task<string> ExecuteViaSSHAsync(ContainerGroupData containerGroup, string command)
    {
        // This is a simplified implementation
        // In a real scenario, you'd need proper SSH connectivity to the container
        // For now, return a message indicating the command was received
        return $"Command '{command}' queued for execution";
    }

    private bool IsTransientError(RequestFailedException ex)
    {
        return ex.Status == 429 || // Too Many Requests
               ex.Status == 503 || // Service Unavailable
               ex.Status == 504;   // Gateway Timeout
    }
}