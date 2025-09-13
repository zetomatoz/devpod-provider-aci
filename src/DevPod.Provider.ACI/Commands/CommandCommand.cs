using DevPod.Provider.ACI.Services;
using Microsoft.Extensions.Logging;

namespace DevPod.Provider.ACI.Commands;

public class CommandCommand(
    ILogger<CommandCommand> logger,
    IProviderOptionsService optionsService,
    IAciService aciService)
{
    public async Task<int> ExecuteAsync(string[] args)
    {
        logger.LogDebug("Executing command in Azure Container Instance");

        try
        {
            var options = optionsService.GetOptions();
            var containerGroupName = options.GetContainerGroupName();

            // Get the command from environment or args
            var command = Environment.GetEnvironmentVariable("COMMAND");
            if (string.IsNullOrWhiteSpace(command))
            {
                Console.Error.WriteLine("No command specified");
                return 1;
            }

            if (args.Length > 0)
            {
                command = string.Join(" ", args);
            }

            logger.LogDebug("Executing command: {Command}", command);

            // Get container endpoint for SSH connection
            var (fqdn, ipAddress) = await aciService.GetContainerEndpointAsync(containerGroupName);

            // For DevPod agent injection, we need to handle specific commands
            if (command.Contains("devpod agent"))
            {
                // DevPod is trying to inject the agent
                // We need to ensure the container is ready and return success
                var status = await aciService.GetContainerGroupStatusAsync(containerGroupName);
                if (status.State == "Running") // todo: avoid magic strings!
                {
                    logger.LogInformation("Container is ready for DevPod agent");

                    // Output connection info for DevPod
                    if (!string.IsNullOrEmpty(fqdn))
                    {
                        Console.WriteLine($"ssh devpod@{fqdn}");
                    }
                    else if (!string.IsNullOrEmpty(ipAddress))
                    {
                        Console.WriteLine($"ssh devpod@{ipAddress}");
                    }

                    return 0;
                }
                else
                {
                    logger.LogWarning("Container not ready: {Status.State}", status.State);
                    return 1;
                }
            }

            // Execute the command
            var result = await aciService.ExecuteCommandAsync(containerGroupName, command);
            Console.WriteLine(result);

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute command");
            Console.Error.WriteLine($"Command execution failed: {ex.Message}");
            return 1;
        }
    }
}
