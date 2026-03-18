using DevPod.Provider.ACI.Models;

namespace DevPod.Provider.ACI.Services;

public interface IAciService
{
    Task<ContainerStatus> CreateContainerGroupAsync(ContainerGroupDefinition definition);
    Task<ContainerStatus> GetContainerGroupStatusAsync(string name);
    Task DeleteContainerGroupAsync(string name);
    Task<ContainerStatus> StartContainerGroupAsync(string name);
    Task<ContainerStatus> StopContainerGroupAsync(string name);
    Task<CommandExecutionResult> ExecuteCommandAsync(
        string containerGroupName,
        string command,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
    Task<int> ExecuteCommandInteractiveAsync(
        string containerGroupName,
        string command,
        Stream stdin,
        Stream stdout,
        Stream stderr,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
    Task<string> GetContainerLogsAsync(string containerGroupName, string containerName);
    Task<(string Fqdn, string IpAddress)> GetContainerEndpointAsync(string name);
}
