using System;
using System.Globalization;
using System.Threading;
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

        var command = args.Length > 0
            ? string.Join(" ", args)
            : Environment.GetEnvironmentVariable("COMMAND");

        if (string.IsNullOrWhiteSpace(command))
        {
            Console.Error.WriteLine("No command specified");
            return 1;
        }

        try
        {
            var options = optionsService.GetOptions();
            var containerGroupName = options.GetContainerGroupName();
            var timeout = ParseTimeout(Environment.GetEnvironmentVariable("COMMAND_TIMEOUT"), logger);

            using var cts = new CancellationTokenSource();
            ConsoleCancelEventHandler handler = (_, e) =>
            {
                logger.LogWarning("Cancellation requested, stopping remote command execution.");
                e.Cancel = true;
                cts.Cancel();
            };
            Console.CancelKeyPress += handler;

            try
            {
                logger.LogDebug("Executing command: {Command}", command);
                var result = await aciService.ExecuteCommandAsync(containerGroupName, command, timeout, cts.Token);

                if (!string.IsNullOrEmpty(result.Stdout))
                {
                    Console.Write(result.Stdout);
                }

                if (!string.IsNullOrEmpty(result.Stderr))
                {
                    Console.Error.Write(result.Stderr);
                }

                logger.LogDebug("Command finished with exit code {ExitCode}", result.ExitCode);
                return result.ExitCode;
            }
            catch (TimeoutException ex)
            {
                logger.LogError(ex, "Remote command timed out after {Timeout}", timeout ?? TimeSpan.FromMinutes(5));
                Console.Error.WriteLine($"Command timed out: {ex.Message}");
                return 124;
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Remote command cancelled.");
                Console.Error.WriteLine("Command execution cancelled.");
                return 130;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to execute command");
                Console.Error.WriteLine($"Command execution failed: {ex.Message}");
                return 1;
            }
            finally
            {
                Console.CancelKeyPress -= handler;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve command execution context");
            Console.Error.WriteLine($"Command execution failed: {ex.Message}");
            return 1;
        }
    }

    private static TimeSpan? ParseTimeout(string? value, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        logger.LogWarning("Ignoring invalid COMMAND_TIMEOUT value: {Value}", value);
        return null;
    }
}
