namespace DevPod.Provider.ACI.Commands;

using DevPod.Provider.ACI.Services;
using Microsoft.Extensions.Logging;

public class StopCommand
{
    private readonly ILogger<StopCommand> _logger;
    private readonly IProviderOptionsService _optionsService;
    private readonly IAciService _aciService;

    public StopCommand(
        ILogger<StopCommand> logger,
        IProviderOptionsService optionsService,
        IAciService aciService)
    {
        _logger = logger;
        _optionsService = optionsService;
        _aciService = aciService;
    }

    public async Task<int> ExecuteAsync()
    {
        _logger.LogInformation("Stopping Azure Container Instance");

        try
        {
            var options = _optionsService.GetOptions();
            var containerGroupName = options.GetContainerGroupName();
            
            var status = await _aciService.StopContainerGroupAsync(containerGroupName);
            
            _logger.LogInformation($"Container group stopped: {containerGroupName}");
            Console.WriteLine($"Container group {containerGroupName} stopped");
            Console.WriteLine($"Status: {status.State}");
            
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop container group");
            Console.Error.WriteLine($"Stop failed: {ex.Message}");
            return 1;
        }
    }
}