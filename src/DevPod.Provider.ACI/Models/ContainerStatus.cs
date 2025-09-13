namespace DevPod.Provider.ACI.Models;

public class ContainerStatus
{
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ProvisioningState { get; set; } = string.Empty;
    public string? Fqdn { get; set; }
    public string? IpAddress { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? FinishTime { get; set; }
    public int? ExitCode { get; set; }
    public string? DetailedStatus { get; set; }
    public Dictionary<string, ContainerInstanceStatus> Containers { get; set; } = [];
}
