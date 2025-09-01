using DevPod.Provider.ACI.Models;

namespace DevPod.Provider.ACI.Services;

public interface IAciService
{
    Task<ContainerStatus> CreateContainerGroupAsync(ContainerGroupDefinition definition);
    Task<ContainerStatus> GetContainerGroupStatusAsync(string name);
    Task DeleteContainerGroupAsync(string name);
    Task<ContainerStatus> StartContainerGroupAsync(string name);
    Task<ContainerStatus> StopContainerGroupAsync(string name);
    Task<string> ExecuteCommandAsync(string containerGroupName, string command);
    Task<string> GetContainerLogsAsync(string containerGroupName, string containerName);
    Task<(string fqdn, string ipAddress)> GetContainerEndpointAsync(string name);
}