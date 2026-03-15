# Command Execution Flow

## End-to-End Walkthrough

1. **Create** – DevPod CLI invokes `create` with a published workspace image. CreateCommand validates that the request is image-backed, rejects unsupported local-path/git/private-network flows, and provisions an ACI container group from that image.
2. **Status lifecycle** – status, start, and stop commands keep DevPod informed about the container’s health so the VS Code extension knows when the environment is ready.
3. **Agent injection** – Once the container is running, DevPod triggers the command handler with a devpod agent payload. The new exec pipeline asks Azure for an exec session, upgrades to the WebSocket URI, and streams stdout/stderr while watching the sentinel exit code. That lets DevPod push its agent into the container without any SSH tunnel yet.
4. **Editor connection** – After the agent is installed, DevPod connects to the running ACI-backed workspace through the injected agent path. In the current release, the provider is intended for published image workspaces rather than general `.devcontainer` feature execution.

From the user perspective: run `devpod up <published-image> --provider <aci-provider>`, let the provider create the ACI container group, then let DevPod inject and use the agent through the ACI exec WebSocket.

## Sequence Overview

```mermaid
sequenceDiagram
    participant DevPodCLI as DevPod CLI
    participant ProviderCLI as devpod-provider-aci
    participant CommandCmd as ExecCommand
    participant AciSvc as AciService
    participant AzureACI as Azure Container Instance
    participant AgentWS as Exec WebSocket

    DevPodCLI->>ProviderCLI: invoke `command`
    ProviderCLI->>CommandCmd: ExecuteAsync(args)
    CommandCmd->>AciSvc: ExecuteCommandAsync(group, command, timeout, token)
    AciSvc->>AzureACI: ExecuteContainerCommandAsync(command script)
    AzureACI-->>AciSvc: ContainerExecResult (WebSocket URI, password)
    AciSvc->>AgentWS: Connect + send password
    AgentWS-->>AciSvc: stdout/stderr frames + sentinel
    AciSvc->>CommandCmd: CommandExecutionResult(stdout, stderr, exitCode)
    CommandCmd->>ProviderCLI: writes stdout/stderr, returns exit status
    ProviderCLI-->>DevPodCLI: exit code / console output
```

## Component Relationships

```mermaid
graph LR
    DevPodClient[DevPod CLI] --> ProviderBinary[devpod-provider-aci CLI]
    ProviderBinary --> CommandLayer[ExecCommand.cs]
    CommandLayer --> ServiceLayer[AciService.cs]
    ServiceLayer --> SDK[Azure ARM SDK<br/>ContainerGroupResource]
    SDK --> AzureACI[(Azure Container Instance)]
    ServiceLayer --> WebSocket[IWebSocketClient<br/>WebSocket stream]
    WebSocket --> ServiceLayer
    ServiceLayer --> CommandLayer
    CommandLayer --> DevPodClient
```
