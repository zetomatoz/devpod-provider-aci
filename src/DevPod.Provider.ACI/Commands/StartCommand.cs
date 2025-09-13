using DevPod.Provider.ACI.Services;
using Microsoft.Extensions.Logging;

namespace DevPod.Provider.ACI.Commands;

public class StartCommand(
    ILogger<StartCommand> logger,
    IProviderOptionsService optionsService,
    IAciService aciService)
{
    public async Task<int> ExecuteAsync()
    {
        logger.LogInformation("Starting Azure Container Instance");

        try
        {
            var options = optionsService.GetOptions();
            var containerGroupName = options.GetContainerGroupName();

            var status = await aciService.StartContainerGroupAsync(containerGroupName);

            logger.LogInformation("Container group started: {ContainerGroupName}", containerGroupName);
            Console.WriteLine($"Container group {containerGroupName} started");
            Console.WriteLine($"Status: {status.State}");

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start container group");
            Console.Error.WriteLine($"Start failed: {ex.Message}");
            return 1;
        }
    }
}
