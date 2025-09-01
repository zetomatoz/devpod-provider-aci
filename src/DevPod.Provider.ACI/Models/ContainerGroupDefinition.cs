namespace DevPod.Provider.ACI.Models;

public class ContainerGroupDefinition
{
    public string Name { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public double CpuCores { get; set; } = 2.0;
    public double MemoryGb { get; set; } = 4.0;
    public int GpuCount { get; set; } = 0;
    public string RestartPolicy { get; set; } = "Never";
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
    public List<int> Ports { get; set; } = new();
    public string? DnsNameLabel { get; set; }
    public string? Command { get; set; }
    public List<string>? Arguments { get; set; }
    public ContainerRegistryCredentials? RegistryCredentials { get; set; }
    public AzureFileShareVolume? FileShareVolume { get; set; }
    public NetworkProfile? NetworkProfile { get; set; }
}

public class ContainerRegistryCredentials
{
    public string Server { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AzureFileShareVolume
{
    public string Name { get; set; } = "workspace";
    public string ShareName { get; set; } = string.Empty;
    public string StorageAccountName { get; set; } = string.Empty;
    public string StorageAccountKey { get; set; } = string.Empty;

    public string MountPath { get; set; } = "/workspace";
}

public class NetworkProfile
{
    public string VnetName { get; set; } = string.Empty;
    public string SubnetName { get; set; } = string.Empty;
    public string SubnetId { get; set; } = string.Empty;
}