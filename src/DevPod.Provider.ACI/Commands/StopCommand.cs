using DevPod.Provider.ACI.Services;
using Microsoft.Extensions.Logging;

namespace DevPod.Provider.ACI.Commands;

public class StopCommand(
    ILogger<StopCommand> logger,
    IProviderOptionsService optionsService,
    IAciService aciService)
{
    public async Task<int> ExecuteAsync()
    {
        logger.LogInformation("Stopping Azure Container Instance");

        try
        {
            var options = optionsService.GetOptions();
            var containerGroupName = options.GetContainerGroupName();

            var status = await aciService.StopContainerGroupAsync(containerGroupName);

            logger.LogInformation("Container group stopped: {ContainerGroupName}", containerGroupName);
            Console.WriteLine($"Container group {containerGroupName} stopped");
            Console.WriteLine($"Status: {status.State}");

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stop container group");
            Console.Error.WriteLine($"Stop failed: {ex.Message}");
            return 1;
        }
    }
}
