using DevPod.Provider.ACI.Services;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;

namespace DevPod.Provider.ACI.Commands;

public class StatusCommand(
    ILogger<StatusCommand> logger,
    IProviderOptionsService optionsService,
    IAciService aciService)
{
    public async Task<int> ExecuteAsync()
    {
        logger.LogDebug("Getting Azure Container Instance status");

        try
        {
            var options = optionsService.GetOptions();
            var containerGroupName = options.GetContainerGroupName();

            var status = await aciService.GetContainerGroupStatusAsync(containerGroupName);

            // DevPod expects specific output format for status
            var devPodStatus = MapToDevPodStatus(status.State);

            Console.WriteLine(devPodStatus);

            // Output additional info as JSON for debugging
            if (Environment.GetEnvironmentVariable("DEVPOD_DEBUG") == "true")
            {
                var json = JsonSerializer.Serialize(status, new JsonSerializerOptions
                {
                    WriteIndented = true,
                });
                logger.LogDebug("Full status: {Json}", json);
            }

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get container group status");
            Console.WriteLine("NotFound");
            return 0; // Return 0 even on error for DevPod compatibility
        }
    }

    private static string MapToDevPodStatus(string aciStatus)
    {
        return aciStatus.ToLower(CultureInfo.InvariantCulture) switch
        {
            "running" => "Running",
            "stopped" => "Stopped",
            "terminated" => "Stopped",
            "pending" => "Busy",
            "creating" => "Busy",
            "updating" => "Busy",
            "failed" => "Error",
            "notfound" => "NotFound",
            _ => "Unknown",
        };
    }
}
