using DevPod.Provider.ACI.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace DevPod.Provider.ACI.Infrastructure;

public class CommandRouter(IServiceProvider serviceProvider)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<CommandRouter> _logger = serviceProvider.GetRequiredService<ILogger<CommandRouter>>();

    public async Task<int> RouteAsync(string[] args)
    {
        if (args.Length == 0)
        {
            _logger.LogError("No command specified");
            return 1;
        }

        var command = args[0].ToLower(CultureInfo.InvariantCulture);
        var commandArgs = args.Skip(1).ToArray();

        _logger.LogDebug("Routing command: {Command}", command);

        try
        {
            return command switch
            {
                Constants.Commands.Init => await ExecuteCommandAsync<InitCommand>(c => c.ExecuteAsync()),
                Constants.Commands.Create => await ExecuteCommandAsync<CreateCommand>(c => c.ExecuteAsync()),
                Constants.Commands.Delete => await ExecuteCommandAsync<DeleteCommand>(c => c.ExecuteAsync()),
                Constants.Commands.Start => await ExecuteCommandAsync<StartCommand>(c => c.ExecuteAsync()),
                Constants.Commands.Stop => await ExecuteCommandAsync<StopCommand>(c => c.ExecuteAsync()),
                Constants.Commands.Status => await ExecuteCommandAsync<StatusCommand>(c => c.ExecuteAsync()),
                Constants.Commands.Command => await ExecuteCommandAsync<CommandCommand>(c => c.ExecuteAsync(commandArgs)),
                _ => HandleUnknownCommand(command),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command '{Command}' failed with exception", command);
            throw;
        }
    }

    private async Task<int> ExecuteCommandAsync<TCommand>(Func<TCommand, Task<int>> executeFunc)
        where TCommand : class
    {
        var command = _serviceProvider.GetRequiredService<TCommand>();
        return await executeFunc(command);
    }

    private int HandleUnknownCommand(string command)
    {
        _logger.LogError("Unknown command: {Command}", command);
        Console.Error.WriteLine($"Unknown command: {command}");
        Console.Error.WriteLine("Valid commands: init, create, delete, start, stop, status, command");
        return 1;
    }
}
