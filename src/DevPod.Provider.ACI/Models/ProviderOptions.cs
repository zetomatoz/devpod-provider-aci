using System.Globalization;

namespace DevPod.Provider.ACI.Models;

public class ProviderOptions
{
    // Azure Authentication
    public string? AzureSubscriptionId { get; set; }
    public string? AzureTenantId { get; set; }
    public string? AzureClientId { get; set; }
    public string? AzureClientSecret { get; set; }

    // Resource Configuration
    public string AzureResourceGroup { get; set; } = "devpod-aci-rg";
    public string AzureRegion { get; set; } = "eastus";
    public double AciCpuCores { get; set; } = 2.0;
    public double AciMemoryGb { get; set; } = 4.0;
    public int AciGpuCount { get; set; }
    public string AciRestartPolicy { get; set; } = "Never";

    // Network Configuration
    public string? AciVnetName { get; set; }
    public string? AciSubnetName { get; set; }
    public string? AciDnsLabel { get; set; }

    // Registry Configuration
    public string? AcrServer { get; set; }
    public string? AcrUsername { get; set; }
    public string? AcrPassword { get; set; }

    // Storage Configuration
    public string? AciStorageAccountName { get; set; }
    public string? AciStorageAccountKey { get; set; }
    public string? AciFileShareName { get; set; }

    // DevPod Agent Configuration
    public string AgentPath { get; set; } = "/home/devpod/.devpod";
    public string InactivityTimeout { get; set; } = "30m";
    public bool InjectGitCredentials { get; set; } = true;
    public bool InjectDockerCredentials { get; set; }

    // DevPod Machine Info
    public string? MachineId { get; set; }
    public string? MachineFolder { get; set; }
    public string? WorkspaceId { get; set; }
    public string? WorkspaceUid { get; set; }

    public static ProviderOptions FromEnvironment()
    {
        var options = new ProviderOptions
        {
            // Azure Authentication
            AzureSubscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID"),
            AzureTenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID"),
            AzureClientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID"),
            AzureClientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET"),
        };

        // Resource Configuration
        options.AzureResourceGroup = Environment.GetEnvironmentVariable("AZURE_RESOURCE_GROUP") ?? options.AzureResourceGroup;
        options.AzureRegion = Environment.GetEnvironmentVariable("AZURE_REGION") ?? options.AzureRegion;

        var cpuCores = Environment.GetEnvironmentVariable("ACI_CPU_CORES");
        if (!string.IsNullOrEmpty(cpuCores) && double.TryParse(cpuCores, out var cpu))
        {
            options.AciCpuCores = cpu;
        }

        var memory = Environment.GetEnvironmentVariable("ACI_MEMORY_GB");
        if (!string.IsNullOrEmpty(memory) && double.TryParse(memory, out var mem))
        {
            options.AciMemoryGb = mem;
        }

        var gpuCount = Environment.GetEnvironmentVariable("ACI_GPU_COUNT");
        if (!string.IsNullOrEmpty(gpuCount) && int.TryParse(gpuCount, out var gpu))
        {
            options.AciGpuCount = gpu;
        }

        options.AciRestartPolicy = Environment.GetEnvironmentVariable("ACI_RESTART_POLICY") ?? options.AciRestartPolicy;

        // Network Configuration
        options.AciVnetName = Environment.GetEnvironmentVariable("ACI_VNET_NAME");
        options.AciSubnetName = Environment.GetEnvironmentVariable("ACI_SUBNET_NAME");
        options.AciDnsLabel = Environment.GetEnvironmentVariable("ACI_DNS_LABEL");

        // Registry Configuration
        options.AcrServer = Environment.GetEnvironmentVariable("ACR_SERVER");
        options.AcrUsername = Environment.GetEnvironmentVariable("ACR_USERNAME");
        options.AcrPassword = Environment.GetEnvironmentVariable("ACR_PASSWORD");

        // Storage Configuration
        options.AciStorageAccountName = Environment.GetEnvironmentVariable("ACI_STORAGE_ACCOUNT_NAME");
        options.AciStorageAccountKey = Environment.GetEnvironmentVariable("ACI_STORAGE_ACCOUNT_KEY");
        options.AciFileShareName = Environment.GetEnvironmentVariable("ACI_FILE_SHARE_NAME") ?? "devpod-workspace";

        // DevPod Agent Configuration
        options.AgentPath = Environment.GetEnvironmentVariable("AGENT_PATH") ?? options.AgentPath;
        options.InactivityTimeout = Environment.GetEnvironmentVariable("INACTIVITY_TIMEOUT") ?? options.InactivityTimeout;

        var injectGit = Environment.GetEnvironmentVariable("INJECT_GIT_CREDENTIALS");
        if (!string.IsNullOrEmpty(injectGit))
        {
            options.InjectGitCredentials = injectGit.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        var injectDocker = Environment.GetEnvironmentVariable("INJECT_DOCKER_CREDENTIALS");
        if (!string.IsNullOrEmpty(injectDocker))
        {
            options.InjectDockerCredentials = injectDocker.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        // DevPod Machine Info
        options.MachineId = Environment.GetEnvironmentVariable("MACHINE_ID");
        options.MachineFolder = Environment.GetEnvironmentVariable("MACHINE_FOLDER");
        options.WorkspaceId = Environment.GetEnvironmentVariable("WORKSPACE_ID");
        options.WorkspaceUid = Environment.GetEnvironmentVariable("WORKSPACE_UID");

        return options;
    }

    public string GetContainerGroupName()
    {
        if (!string.IsNullOrEmpty(MachineId))
        {
            // Ensure the name is valid for ACI (lowercase, alphanumeric, hyphens)
            return $"devpod-{MachineId}".ToLower(CultureInfo.InvariantCulture).Replace("_", "-");
        }

        return $"devpod-{Guid.NewGuid().ToString()[..8]}".ToLower(CultureInfo.InvariantCulture);
    }
}
