namespace DevPod.Provider.ACI.Models;

public class AzureFileShareVolume
{
    public string Name { get; set; } = "workspace";
    public string ShareName { get; set; } = string.Empty;
    public string StorageAccountName { get; set; } = string.Empty;
    public string StorageAccountKey { get; set; } = string.Empty;
    public string MountPath { get; set; } = "/workspace";
}
