namespace DevPod.Provider.ACI.Commands;

using DevPod.Provider.ACI.Services;
using Microsoft.Extensions.Logging;

public class DeleteCommand
{
    private readonly ILogger<DeleteCommand> _logger;
    private readonly IProviderOptionsService _optionsService;
    private readonly IAciService _aciService;

    public DeleteCommand(
        ILogger<DeleteCommand> logger,
        IProviderOptionsService optionsService,
        IAciService aciService)
    {
        _logger = logger;
        _optionsService = optionsService;
        _aciService = aciService;
    }

    public async Task<int> ExecuteAsync()
    {
        _logger.LogInformation("Deleting Azure Container Instance");

        try
        {
            var options = _optionsService.GetOptions();
            var containerGroupName = options.GetContainerGroupName();
            
            await _aciService.DeleteContainerGroupAsync(containerGroupName);
            
            _logger.LogInformation($"Container group deleted: {containerGroupName}");
            Console.WriteLine($"Container group {containerGroupName} deleted successfully");
            
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete container group");
            Console.Error.WriteLine($"Delete failed: {ex.Message}");
            return 1;
        }
    }
}