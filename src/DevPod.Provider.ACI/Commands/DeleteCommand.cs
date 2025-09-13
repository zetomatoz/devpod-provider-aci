using DevPod.Provider.ACI.Services;
using Microsoft.Extensions.Logging;

namespace DevPod.Provider.ACI.Commands;

public class DeleteCommand(
    ILogger<DeleteCommand> logger,
    IProviderOptionsService optionsService,
    IAciService aciService)
{
    public async Task<int> ExecuteAsync()
    {
        logger.LogInformation("Deleting Azure Container Instance");

        try
        {
            var options = optionsService.GetOptions();
            var containerGroupName = options.GetContainerGroupName();

            await aciService.DeleteContainerGroupAsync(containerGroupName);

            logger.LogInformation("Container group deleted: {ContainerGroupName}", containerGroupName);
            Console.WriteLine($"Container group {containerGroupName} deleted successfully");

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete container group");
            Console.Error.WriteLine($"Delete failed: {ex.Message}");
            return 1;
        }
    }
}
