namespace DevPod.Provider.ACI.Models;

public class ContainerGroupDefinition
{
    public string Name { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public double CpuCores { get; set; } = 2.0;
    public double MemoryGb { get; set; } = 4.0;
    public int GpuCount { get; set; }
    public string RestartPolicy { get; set; } = "Never";
    public Dictionary<string, string> EnvironmentVariables { get; set; } = [];
    public List<int> Ports { get; set; } = [];
    public string? DnsNameLabel { get; set; }
    public string? Command { get; set; }
    public List<string>? Arguments { get; set; }
    public ContainerRegistryCredentials? RegistryCredentials { get; set; }
    public AzureFileShareVolume? FileShareVolume { get; set; }
    public NetworkProfile? NetworkProfile { get; set; }
}
