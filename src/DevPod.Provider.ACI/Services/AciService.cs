using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ContainerInstance;
using Azure.ResourceManager.ContainerInstance.Models;
using Azure.ResourceManager.Models;
using Azure.ResourceManager.Resources;
using DevPod.Provider.ACI.Models;
using DevPod.Provider.ACI.Infrastructure;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
namespace DevPod.Provider.ACI.Services;

public class AciService : IAciService
{
    // Sentinel string to indicate exit code in command output
    // This should be unique enough to avoid collisions
    // The StripExitInfo method could fail if the sentinel appears in legitimate command output.
    // Recommendation: we use a more unique sentinel
    private const string ExitSentinel = "__ACI_EXIT_CODE_SENTINEL_D9F3A1B2__:";

    // todo Consider making it configurable or larger.
    private const int WebSocketBufferSize = 64 * 1024;

    private const byte WebSocketChannelStdout = 1;
    private const byte WebSocketChannelStderr = 2;
    private const byte WebSocketChannelError = 3;     

    private readonly ILogger<AciService> _logger;
    private readonly IAuthenticationService _authService;
    private readonly IProviderOptionsService _optionsService;
    private readonly IWebSocketClientFactory _webSocketClientFactory;
    private readonly AsyncRetryPolicy _retryPolicy;

    public AciService(
        ILogger<AciService> logger,
        IAuthenticationService authService,
        IProviderOptionsService optionsService,
        IWebSocketClientFactory webSocketClientFactory)
    {
        _logger = logger;
        _authService = authService;
        _optionsService = optionsService;
        _webSocketClientFactory = webSocketClientFactory;

        // Configure retry policy for transient failures
        _retryPolicy = Policy
            .Handle<RequestFailedException>(IsTransientError)
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry {RetryCount} after {Timespan} seconds", retryCount, timespan.TotalSeconds);
                });
    }

    public async Task<ContainerStatus> CreateContainerGroupAsync(ContainerGroupDefinition definition)
    {
        _logger.LogInformation("Creating container group: {ContainerGroupName}", definition.Name);

        var armClient = await _authService.GetArmClientAsync();
        var options = _optionsService.GetOptions();

        // Get or create resource group
        var resourceGroup = await GetOrCreateResourceGroupAsync(armClient, options);

        // Build container group data
        var containerGroupData = await BuildContainerGroupDataAsync(definition, resourceGroup);

        // Create container group
        var containerGroupCollection = resourceGroup.GetContainerGroups();

        var operation = await _retryPolicy.ExecuteAsync(async () =>
            await containerGroupCollection.CreateOrUpdateAsync(
                WaitUntil.Completed,
                definition.Name,
                containerGroupData));

        var containerGroup = operation.Value;

        _logger.LogInformation("Container group created: {ContainerGroupName}", containerGroup.Data.Name);

        return MapToContainerStatus(containerGroup.Data);
    }

    public async Task<ContainerStatus> GetContainerGroupStatusAsync(string name)
    {
        _logger.LogDebug("Getting status for container group: {ContainerGroupName}", name);

        var containerGroup = await GetContainerGroupAsync(name);
        return containerGroup == null
            ? new ContainerStatus
            {
                Name = name,
                State = "NotFound",
                ProvisioningState = "NotFound",
            }
            : MapToContainerStatus(containerGroup.Data);
    }

    public async Task DeleteContainerGroupAsync(string name)
    {
        _logger.LogInformation("Deleting container group: {ContainerGroupName}", name);

        var containerGroup = await GetContainerGroupAsync(name);
        if (containerGroup != null)
        {
            await containerGroup.DeleteAsync(WaitUntil.Completed);
            _logger.LogInformation("Container group deleted: {ContainerGroupName}", name);
        }
        else
        {
            _logger.LogWarning("Container group not found: {ContainerGroupName}", name);
        }
    }

    public async Task<ContainerStatus> StartContainerGroupAsync(string name)
    {
        _logger.LogInformation("Starting container group: {ContainerGroupName}", name);

        var containerGroup = await GetContainerGroupAsync(name) ?? throw new InvalidOperationException($"Container group not found: {name}");
        await containerGroup.StartAsync(WaitUntil.Completed);

        // Refresh the container group data
        containerGroup = await GetContainerGroupAsync(name);
        return MapToContainerStatus(containerGroup!.Data);
    }

    public async Task<ContainerStatus> StopContainerGroupAsync(string name)
    {
        _logger.LogInformation("Stopping container group: {ContainerGroupName}", name);

        var containerGroup = await GetContainerGroupAsync(name) ?? throw new InvalidOperationException($"Container group not found: {name}");
        await containerGroup.StopAsync();

        // Refresh the container group data
        containerGroup = await GetContainerGroupAsync(name);
        return MapToContainerStatus(containerGroup!.Data);
    }

    /// <summary>
    /// Executes a command inside the specified container group via WebSocket.
    /// </summary>
    /// <param name="containerGroupName">The name of the container group.</param>
    /// <param name="command">The command to execute.</param>
    /// <param name="timeout">Optional timeout for command execution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of command execution including exit code and output.</returns>
    public async Task<CommandExecutionResult> ExecuteCommandAsync(
        string containerGroupName,
        string command,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerGroupName);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        _logger.LogDebug("Executing command in container group: {ContainerGroupName}", containerGroupName);

        var containerGroup = await GetContainerGroupAsync(containerGroupName) ??
            throw new InvalidOperationException($"Container group not found: {containerGroupName}");

        if (containerGroup.Data.Containers.Count == 0)
        {
            throw new InvalidOperationException($"Container group '{containerGroupName}' has no containers to execute against.");
        }

        var container = containerGroup.Data.Containers[0];
        var containerName = container.Name;

        if (string.IsNullOrWhiteSpace(containerName))
        {
            throw new InvalidOperationException($"Unable to resolve container name for container group '{containerGroupName}'.");
        }

        var state = container.InstanceView?.CurrentState?.State ?? "Unknown";
        if (state.Equals("Terminated", StringComparison.OrdinalIgnoreCase)
         || state.Equals("Failed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Cannot execute command: container is in '{state}' state");
        }

        if (!string.Equals(container.InstanceView?.CurrentState?.State, "Running", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Executing command while container {ContainerName} is in state {State}",
                containerName,
                container.InstanceView?.CurrentState?.State ?? "Unknown");
        }

        var execCommand = BuildExecCommand(command);
        var execContent = new ContainerExecContent
        {
            Command = execCommand,
            TerminalSize = new ContainerExecRequestTerminalSize
            {
                Cols = 200,
                Rows = 40,
            },
        };

        var execResultResponse = await containerGroup.ExecuteContainerCommandAsync(containerName, execContent, cancellationToken);
        var execResult = execResultResponse.Value;
        if (execResult.WebSocketUri is null || string.IsNullOrWhiteSpace(execResult.Password))
        {
            throw new InvalidOperationException("Azure Container Instance exec response did not include WebSocket connection info.");
        }

        var effectiveTimeout = timeout is { TotalMilliseconds: > 0 }
            ? timeout.Value
            : TimeSpan.FromMinutes(5);

        return await RunExecSessionAsync(execResult, effectiveTimeout, cancellationToken);
    }

    public async Task<string> GetContainerLogsAsync(string containerGroupName, string containerName)
    {
        _logger.LogDebug("Getting logs for container: {ContainerName} in group: {ContainerGroupName}", containerName, containerGroupName);

        var containerGroup = await GetContainerGroupAsync(containerGroupName) ??
        throw new InvalidOperationException($"Container group not found: {containerGroupName}");

        var container = containerGroup.Data.Containers.FirstOrDefault(c => c.Name == containerName);
        if (container == null)
        {
            containerName = containerGroup.Data.Containers.First().Name;
        }

        var logsResult = await containerGroup.GetContainerLogsAsync(containerName);
        return logsResult.Value.Content ?? string.Empty;
    }

    public async Task<(string Fqdn, string IpAddress)> GetContainerEndpointAsync(string name)
    {
        var containerGroup = await GetContainerGroupAsync(name);
        if (containerGroup == null)
        {
            return (string.Empty, string.Empty);
        }

        var fqdn = containerGroup.Data.IPAddress?.Fqdn ?? string.Empty;
        var ipAddress = containerGroup.Data.IPAddress?.IP?.ToString() ?? string.Empty;

        return (fqdn, ipAddress);
    }

    private async Task<ResourceGroupResource> GetOrCreateResourceGroupAsync(ArmClient armClient, ProviderOptions options)
    {
        var subscription = await armClient.GetDefaultSubscriptionAsync();
        var resourceGroups = subscription.GetResourceGroups();

        try
        {
            var resourceGroup = await resourceGroups.GetAsync(options.AzureResourceGroup);
            _logger.LogDebug("Using existing resource group: {AzureResourceGroup}", options.AzureResourceGroup);
            return resourceGroup.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogInformation("Creating resource group: {AzureResourceGroup}", options.AzureResourceGroup);
            var location = new AzureLocation(options.AzureRegion);
            var resourceGroupData = new ResourceGroupData(location);
            var operation = await resourceGroups.CreateOrUpdateAsync(
                WaitUntil.Completed,
                options.AzureResourceGroup,
                resourceGroupData);
            return operation.Value;
        }
    }

    private async Task<ContainerGroupData> BuildContainerGroupDataAsync(
        ContainerGroupDefinition definition,
        ResourceGroupResource resourceGroup)
    {
        var location = new AzureLocation(definition.Location);
        var options = _optionsService.GetOptions();

        // Create container configuration
        var resources = new ContainerResourceRequirements(
            new ContainerResourceRequestsContent(definition.MemoryGb, definition.CpuCores));
        var container = new ContainerInstanceContainer(definition.Name, definition.Image, resources);

        // Add GPU if requested
        if (definition.GpuCount > 0)
        {
            resources.Requests.Gpu = new ContainerGpuResourceInfo(definition.GpuCount, ContainerGpuSku.K80);
        }

        // Add environment variables
        foreach (var envVar in definition.EnvironmentVariables)
        {
            container.EnvironmentVariables.Add(new ContainerEnvironmentVariable(envVar.Key)
            {
                Value = envVar.Value,
            });
        }

        // Add DevPod specific environment variables
        container.EnvironmentVariables.Add(new ContainerEnvironmentVariable("DEVPOD")
        {
            Value = "true",
        });

        // Add ports
        foreach (var port in definition.Ports)
        {
            container.Ports.Add(new ContainerPort(port));
        }

        // Set command if provided
        if (!string.IsNullOrEmpty(definition.Command))
        {
            container.Command.Add(definition.Command);
            if (definition.Arguments != null)
            {
                foreach (var arg in definition.Arguments)
                {
                    container.Command.Add(arg);
                }
            }
        }

        // Create container group
        var containerGroupData = new ContainerGroupData(location, [container], ContainerInstanceOperatingSystemType.Linux)
        {
            RestartPolicy = Enum.Parse<ContainerGroupRestartPolicy>(definition.RestartPolicy, true),
        };

        // Configure identity for ACR pull if using Managed Identity
        var acrAuthMode = (options.AcrAuthMode ?? "ManagedIdentity").Trim();
        if (acrAuthMode.Equals("ManagedIdentity", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(options.UserAssignedIdentityResourceId))
            {
                containerGroupData.Identity = new ManagedServiceIdentity(ManagedServiceIdentityType.UserAssigned);
                containerGroupData.Identity.UserAssignedIdentities.Add(
                    new ResourceIdentifier(options.UserAssignedIdentityResourceId),
                    new UserAssignedIdentity());
            }
            else
            {
                containerGroupData.Identity = new ManagedServiceIdentity(ManagedServiceIdentityType.SystemAssigned);
            }
        }

        // Add registry credentials if provided (not needed for Managed Identity)
        if (!acrAuthMode.Equals("ManagedIdentity", StringComparison.OrdinalIgnoreCase) && definition.RegistryCredentials != null)
        {
            containerGroupData.ImageRegistryCredentials.Add(new ContainerGroupImageRegistryCredential(
                definition.RegistryCredentials.Server)
            {
                Username = definition.RegistryCredentials.Username,
                Password = definition.RegistryCredentials.Password,
            });
        }

        // Configure networking
        // todo review to understand better this subnet concept
        if (definition.NetworkProfile != null && !string.IsNullOrEmpty(definition.NetworkProfile.SubnetId))
        {
            // Private network configuration
            containerGroupData.SubnetIds.Add(new ContainerGroupSubnetId(new ResourceIdentifier(definition.NetworkProfile.SubnetId)));
        }
        else
        {
            // Public IP configuration
            containerGroupData.IPAddress = new ContainerGroupIPAddress(
                [.. definition.Ports.Select(p => new ContainerGroupPort(p))],
                ContainerGroupIPAddressType.Public);

            if (!string.IsNullOrEmpty(definition.DnsNameLabel))
            {
                containerGroupData.IPAddress.DnsNameLabel = definition.DnsNameLabel;
            }
        }

        // Add Azure File Share volume if configured
        if (definition.FileShareVolume != null)
        {
            var volume = new ContainerVolume(definition.FileShareVolume.Name)
            {
                AzureFile = new ContainerInstanceAzureFileVolume(
                    definition.FileShareVolume.ShareName,
                    definition.FileShareVolume.StorageAccountName)
                {
                    StorageAccountKey = definition.FileShareVolume.StorageAccountKey,
                },
            };
            containerGroupData.Volumes.Add(volume);

            // Mount the volume in the container
            container.VolumeMounts.Add(new ContainerVolumeMount(
                definition.FileShareVolume.Name,
                definition.FileShareVolume.MountPath));
        }

        return containerGroupData;
    }

    protected virtual async Task<ContainerGroupResource?> GetContainerGroupAsync(string name)
    {
        try
        {
            var armClient = await _authService.GetArmClientAsync();
            var options = _optionsService.GetOptions();
            var subscription = await armClient.GetDefaultSubscriptionAsync();
            var resourceGroup = await subscription.GetResourceGroupAsync(options.AzureResourceGroup);
            return await resourceGroup.Value.GetContainerGroupAsync(name);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private static ContainerStatus MapToContainerStatus(ContainerGroupData data)
    {
        var status = new ContainerStatus
        {
            Name = data.Name,
            ProvisioningState = data.ProvisioningState ?? "Unknown",
            State = GetContainerGroupState(data),
            Fqdn = data.IPAddress?.Fqdn,
            IpAddress = data.IPAddress?.IP.ToString(),
        };

        foreach (var container in data.Containers)
        {
            var instanceStatus = new ContainerInstanceStatus
            {
                Name = container.Name,
                State = container.InstanceView?.CurrentState?.State ?? "Unknown",
                StartTime = container.InstanceView?.CurrentState?.StartOn?.DateTime,
                FinishTime = container.InstanceView?.CurrentState?.FinishOn?.DateTime,
                ExitCode = container.InstanceView?.CurrentState?.ExitCode,
                DetailedStatus = container.InstanceView?.CurrentState?.DetailStatus,
            };

            status.Containers[container.Name] = instanceStatus;
        }

        return status;
    }

    private static string GetContainerGroupState(ContainerGroupData data)
    {
        // Check provisioning state first
        if (data.ProvisioningState == "Failed")
        {
            return "Failed";
        }

        // Check container states
        var containerStates = data.Containers
            .Select(c => c.InstanceView?.CurrentState?.State)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        if (containerStates.Any(s => s == "Running"))
        {
            return "Running";
        }

        return containerStates.Any(s => s == "Terminated") ? "Stopped" : containerStates.Any(s => s == "Waiting") ? "Pending" : "Unknown";
    }

    private async Task<CommandExecutionResult> RunExecSessionAsync(ContainerExecResult execResult, TimeSpan timeout, CancellationToken cancellationToken)
    {
        await using var webSocket = _webSocketClientFactory.Create();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await webSocket.ConnectAsync(execResult.WebSocketUri!, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Timed out connecting to container exec endpoint after {timeout}.", ex);
        }

        var passwordPayload = Encoding.UTF8.GetBytes(execResult.Password);
        if (webSocket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException($"WebSocket is not open. State: {webSocket.State}");
        }
        await webSocket.SendAsync(passwordPayload, WebSocketMessageType.Text, true, timeoutCts.Token).ConfigureAwait(false);

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        int? exitCode = null;

        byte[]? buffer = null;
        try
        {
            buffer = ArrayPool<byte>.Shared.Rent(WebSocketBufferSize);
            using var messageStream = new MemoryStream();

            while (true)
            {
                WebSocketReceiveResult receiveResult;
                try
                {
                    receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException($"Command execution timed out after {timeout}.", ex);
                }

                if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Completed", CancellationToken.None).ConfigureAwait(false);
                    break;
                }

                if (receiveResult.Count > 0)
                {
                    messageStream.Write(buffer, 0, receiveResult.Count);
                }

                if (!receiveResult.EndOfMessage)
                {
                    continue;
                }

                var messageBytes = messageStream.ToArray();
                messageStream.SetLength(0);

                ProcessWebSocketMessage(receiveResult.MessageType, messageBytes, stdout, stderr, ref exitCode);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        var stdoutInfo = StripExitInfo(stdout.ToString());
        var stderrInfo = StripExitInfo(stderr.ToString());

        var finalExitCode = exitCode
            ?? stdoutInfo.ExitCode
            ?? stderrInfo.ExitCode
            ?? 0;

        return new CommandExecutionResult(
            finalExitCode,
            stdoutInfo.Text,
            stderrInfo.Text);
    }

    private static void ProcessWebSocketMessage(
        WebSocketMessageType messageType,
        ReadOnlySpan<byte> payload,
        StringBuilder stdout,
        StringBuilder stderr,
        ref int? exitCode)
    {
        if (payload.IsEmpty)
        {
            return;
        }

        if (messageType == WebSocketMessageType.Binary)
        {
            var channel = payload[0];
            var remaining = payload.Length > 1 ? payload[1..] : ReadOnlySpan<byte>.Empty;
            var text = remaining.IsEmpty ? string.Empty : Encoding.UTF8.GetString(remaining);

            switch (channel)
            {
                case WebSocketChannelStdout:
                    stdout.Append(text);
                    exitCode ??= TryParseExitCodeFromMessage(text);
                    break;
                case WebSocketChannelStderr:
                    stderr.Append(text);
                    exitCode ??= TryParseExitCodeFromMessage(text);
                    break;
                case WebSocketChannelError:
                    stderr.Append(text);
                    exitCode ??= TryParseExitCodeFromMessage(text);
                    break;
                default:
                    stdout.Append(text);
                    break;
            }

            return;
        }

        var message = Encoding.UTF8.GetString(payload);
        stdout.Append(message);
        exitCode ??= TryParseExitCodeFromMessage(message);
    }

    private static (string Text, int? ExitCode) StripExitInfo(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return (text, null);
        }

        var index = text.LastIndexOf(ExitSentinel, StringComparison.Ordinal);
        if (index < 0)
        {
            return (text, null);
        }

        var start = index + ExitSentinel.Length;
        var end = start;

        while (end < text.Length && char.IsDigit(text[end]))
        {
            end++;
        }

        if (!int.TryParse(text[start..end], NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
        {
            return (text, null);
        }

        var prefixEnd = index;
        if (prefixEnd > 0 && text[prefixEnd - 1] == '\n')
        {
            prefixEnd -= 1;
        }

        var prefix = text[..prefixEnd];
        var suffix = text[end..];
        if (suffix.StartsWith("\n", StringComparison.Ordinal))
        {
            suffix = suffix[1..];
        }

        return (prefix + suffix, code);
    }

    private static int? TryParseExitCodeFromMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var trimmed = message.Trim();

        if (trimmed.StartsWith(ExitSentinel, StringComparison.Ordinal))
        {
            var numericPart = trimmed[ExitSentinel.Length..];
            if (int.TryParse(numericPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sentinelCode))
            {
                return sentinelCode;
            }
        }

        return null;
    }

    private string BuildExecCommand(string command)
    {
        var scriptBuilder = new StringBuilder();
        scriptBuilder.AppendLine("set -o pipefail >/dev/null 2>&1 || true");
        scriptBuilder.AppendLine(command);
        scriptBuilder.AppendLine("status=$?");
        scriptBuilder.AppendLine($"printf \"\\n{ExitSentinel}%d\\n\" \"$status\"");
        scriptBuilder.AppendLine("exit $status");

        var script = scriptBuilder.ToString();
        var escaped = EscapeForSingleQuotedString(script);
        return $"/bin/sh -c '{escaped}'";
    }

    private string EscapeForSingleQuotedString(string value)
    {
        // Log a warning if the command contains suspicious patterns
        if (value.Contains("'\"'\"'") || value.Contains("${"))
        {
            _logger.LogWarning("Command contains potentially problematic shell metacharacters");
        }

        return value.Replace("'", "'\"'\"'");
    }

    private bool IsTransientError(RequestFailedException ex)
    {
        return ex.Status is 429 or // Too Many Requests
               503 or // Service Unavailable
               504;   // Gateway Timeout
    }
}
