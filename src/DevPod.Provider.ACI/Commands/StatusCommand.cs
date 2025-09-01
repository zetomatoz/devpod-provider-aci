namespace DevPod.Provider.ACI.Commands;

using DevPod.Provider.ACI.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public class StatusCommand
{
    private readonly ILogger<StatusCommand> _logger;
    private readonly IProviderOptionsService _optionsService;
    private readonly IAciService _aciService;

    public StatusCommand(
        ILogger<StatusCommand> logger,
        IProviderOptionsService optionsService,
        IAciService aciService)
    {
        _logger = logger;
        _optionsService = optionsService;
        _aciService = aciService;
    }

    public async Task<int> ExecuteAsync()
    {
        _logger.LogDebug("Getting Azure Container Instance status");

        try
        {
            var options = _optionsService.GetOptions();
            var containerGroupName = options.GetContainerGroupName();
            
            var status = await _aciService.GetContainerGroupStatusAsync(containerGroupName);
            
            // DevPod expects specific output format for status
            var devPodStatus = MapToDevPodStatus(status.State);
            
            Console.WriteLine(devPodStatus);
            
            // Output additional info as JSON for debugging
            if (Environment.GetEnvironmentVariable("DEVPOD_DEBUG") == "true")
            {
                var json = JsonSerializer.Serialize(status, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                _logger.LogDebug($"Full status: {json}");
            }
            
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get container group status");
            Console.WriteLine("NotFound");
            return 0; // Return 0 even on error for DevPod compatibility
        }
    }

    private string MapToDevPodStatus(string aciStatus)
    {
        return aciStatus.ToLower() switch
        {
            "running" => "Running",
            "stopped" => "Stopped",
            "terminated" => "Stopped",
            "pending" => "Busy",
            "creating" => "Busy",
            "updating" => "Busy",
            "failed" => "Error",
            "notfound" => "NotFound",
            _ => "Unknown"
        };
    }
}
