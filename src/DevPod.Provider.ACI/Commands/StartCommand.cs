namespace DevPod.Provider.ACI.Commands;

using DevPod.Provider.ACI.Services;
using Microsoft.Extensions.Logging;

public class StartCommand
{
    private readonly ILogger<StartCommand> _logger;
    private readonly IProviderOptionsService _optionsService;
    private readonly IAciService _aciService;

    public StartCommand(
        ILogger<StartCommand> logger,
        IProviderOptionsService optionsService,
        IAciService aciService)
    {
        _logger = logger;
        _optionsService = optionsService;
        _aciService = aciService;
    }

    public async Task<int> ExecuteAsync()
    {
        _logger.LogInformation("Starting Azure Container Instance");

        try
        {
            var options = _optionsService.GetOptions();
            var containerGroupName = options.GetContainerGroupName();
            
            var status = await _aciService.StartContainerGroupAsync(containerGroupName);
            
            _logger.LogInformation($"Container group started: {containerGroupName}");
            Console.WriteLine($"Container group {containerGroupName} started");
            Console.WriteLine($"Status: {status.State}");
            
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start container group");
            Console.Error.WriteLine($"Start failed: {ex.Message}");
            return 1;
        }
    }
}