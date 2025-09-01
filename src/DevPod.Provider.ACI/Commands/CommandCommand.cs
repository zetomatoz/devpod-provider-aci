using DevPod.Provider.ACI.Services;
using Microsoft.Extensions.Logging;
using System.Text;

namespace DevPod.Provider.ACI.Commands;

public class CommandCommand
{
    private readonly ILogger<CommandCommand> _logger;
    private readonly IProviderOptionsService _optionsService;
    private readonly IAciService _aciService;

    public CommandCommand(
        ILogger<CommandCommand> logger,
        IProviderOptionsService optionsService,
        IAciService aciService)
    {
        _logger = logger;
        _optionsService = optionsService;
        _aciService = aciService;
    }

    public async Task<int> ExecuteAsync(string[] args)
    {
        _logger.LogDebug("Executing command in Azure Container Instance");

        try
        {
            var options = _optionsService.GetOptions();
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

            _logger.LogDebug($"Executing command: {command}");

            // Get container endpoint for SSH connection
            var (fqdn, ipAddress) = await _aciService.GetContainerEndpointAsync(containerGroupName);

            // For DevPod agent injection, we need to handle specific commands
            if (command.Contains("devpod agent"))
            {
                // DevPod is trying to inject the agent
                // We need to ensure the container is ready and return success
                var status = await _aciService.GetContainerGroupStatusAsync(containerGroupName);
                if (status.State == "Running") //todo: avoid magic strings!
                {
                    _logger.LogInformation("Container is ready for DevPod agent");
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
                    _logger.LogWarning($"Container not ready: {status.State}");
                    return 1;
                }
            }

            // Execute the command
            var result = await _aciService.ExecuteCommandAsync(containerGroupName, command);
            Console.WriteLine(result);

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute command");
            Console.Error.WriteLine($"Command execution failed: {ex.Message}");
            return 1;
        }
    }
}