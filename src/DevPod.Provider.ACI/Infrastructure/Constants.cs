namespace DevPod.Provider.ACI.Infrastructure;

public static class Constants
{
    public const string ProviderName = "azure-container-instance";
    public const string ProviderVersion = "0.1.0";

    public static class Commands
    {
        public const string Init = "init";
        public const string Create = "create";
        public const string Delete = "delete";
        public const string Start = "start";
        public const string Stop = "stop";
        public const string Status = "status";
        public const string Command = "command";
    }

    public static class EnvironmentVariables
    {
        public const string DevPodDebug = "DEVPOD_DEBUG";
        public const string MachineId = "MACHINE_ID";
        public const string MachineFolder = "MACHINE_FOLDER";
        public const string WorkspaceId = "WORKSPACE_ID";
        public const string WorkspaceUid = "WORKSPACE_UID";
        public const string WorkspaceImage = "WORKSPACE_IMAGE";
        public const string WorkspaceSource = "WORKSPACE_SOURCE";
        public const string Command = "COMMAND";
    }

    public static class Defaults
    {
        public const string ResourceGroupName = "devpod-aci-rg";
        public const string Region = "eastus";
        public const double CpuCores = 2.0;
        public const double MemoryGb = 4.0;
        public const string RestartPolicy = "Never";
        public const string AgentPath = "/home/devpod/.devpod";
        public const string InactivityTimeout = "30m";
        public const string BaseImage = "mcr.microsoft.com/devcontainers/base:ubuntu";
    }
}
