namespace DevPod.Provider.ACI.Models;

public class ContainerInstanceStatus
{
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public DateTime? FinishTime { get; set; }
    public int? ExitCode { get; set; }
    public string? DetailedStatus { get; set; }
}
