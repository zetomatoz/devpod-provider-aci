namespace DevPod.Provider.ACI.Models;

public class NetworkProfile
{
    public string VnetName { get; set; } = string.Empty;
    public string SubnetName { get; set; } = string.Empty;
    public string SubnetId { get; set; } = string.Empty;
}
