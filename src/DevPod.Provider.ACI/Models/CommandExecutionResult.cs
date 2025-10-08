namespace DevPod.Provider.ACI.Models;

/// <summary>
/// Represents the outcome of executing a command inside an Azure Container Instance.
/// </summary>
public record CommandExecutionResult(int ExitCode, string Stdout, string Stderr);
