# DevPod Provider for Azure Container Instances

Run image-backed DevPod workspaces on Azure Container Instances (ACI).

## Current Scope

Supported in the current direct-ACI release:

- Published workspace images via `WORKSPACE_IMAGE`
- Public ACI deployments with optional DNS labels
- Azure File Share mounts
- Azure Container Registry authentication through Managed Identity, Key Vault, or username/password
- DevPod agent injection through the ACI exec WebSocket

Not supported in this release:

- Local-path or git workspaces
- General `.devcontainer` feature execution, bind mounts, or `postCreateCommand`
- Private VNet/subnet deployments

## Prerequisites

- DevPod CLI installed: [Installation Guide](https://devpod.sh/docs/getting-started/install)
- Azure subscription with ACI enabled
- Azure CLI logged in, or service principal credentials
- .NET 8 SDK for local builds
- Python 3 for manifest rendering during local provider packaging

If the subscription has not used ACI before, register the resource provider once:

```bash
az provider register --namespace Microsoft.ContainerInstance
```

## Installation

### Install from a release

```bash
devpod provider add github.com/your-org/devpod-provider-aci
```

### Build from source

```bash
git clone https://github.com/zetomatoz/devpod-provider-aci
cd devpod-provider-aci

# Build and package the provider for local DevPod installation.
# This does more than dotnet build:
# - publishes the provider binary for each supported platform
# - computes SHA256 checksums
# - renders dist/provider-local.yaml from provider.yaml
./hack/build.sh

# Register the rendered local manifest with DevPod.
devpod provider add ./dist/provider-local.yaml --name aci-local
```

`dotnet build` is still useful for normal development and compilation checks, but it does not produce the rendered DevPod manifest that `devpod provider add` expects.

## Hello World Smoke Test

The sample smoke test is image-based. It does not use `devpod up ./samples/dotnet-hello-world`.

```bash
export AZURE_SUBSCRIPTION_ID="<subscription-id>"
export AZURE_RESOURCE_GROUP="devpod-aci-e2e"
export AZURE_REGION="westus2"

devpod up ghcr.io/<your-org>/devpod-provider-aci-hello-world:latest \
  --id aci-hello \
  --provider aci-local \
  --provider-option WORKSPACE_IMAGE=ghcr.io/<your-org>/devpod-provider-aci-hello-world:latest \
  --ide none
```

Validate the running workspace:

```bash
devpod ssh aci-hello
ls -l /tmp/devpod
curl -fsS http://127.0.0.1:8080/health
curl -fsS http://127.0.0.1:8080/
exit
```

The detailed walkthrough lives in [tests/e2e/README.md](tests/e2e/README.md).

## AKS Smoke Test

This repository also includes an AKS-based smoke path that uses DevPod's
first-party `kubernetes` provider instead of the custom ACI provider.

Use this when you want to validate the Kubernetes direction end to end with the
smallest possible workspace.

Important notes before you start:

- use `samples/aks-smoke` for the first test, not `devpod up .`
- the repository root currently contains large local build outputs, so using the
  full repo as a local-path workspace will upload far more data than needed
- if you want the provider and workspace to appear in the normal DevPod UI, use
  your normal DevPod home instead of the temporary `/tmp/devpod-aks-home`

### Prerequisites

- Azure CLI installed and logged in
- DevPod CLI installed
- `kubectl` installed
- `AZURE_SUBSCRIPTION_ID` set
- `AZURE_REGION` set, or set `AKS_LOCATION`

Confirm Azure login:

```bash
az account show
```

### Step 1: Set Test Variables

From the repository root:

```bash
cd "$HOME/Sites/devpod-provider-aci"  # or your local repo path

export AKS_RESOURCE_GROUP="devpod-aks-rg"
export AKS_NAME="devpod-aks"
export AZURE_REGION="${AZURE_REGION:-westus2}"
```

The AKS helpers intentionally require `AKS_RESOURCE_GROUP`. They do not fall
back to `AZURE_RESOURCE_GROUP`, because the direct ACI flow in this repository
commonly uses a different resource group such as `devpod-aci-e2e`.

If you want the DevPod desktop UI to show the provider and workspace, also run:

```bash
export DEVPOD_HOME="$HOME/.devpod"
```

If you skip that line, the smoke helper defaults to `/tmp/devpod-aks-home`,
which keeps the test isolated but will not appear in the normal UI.

### Step 2: Create A Temporary SSH Key

AKS requires an SSH public key for the Linux node pool.

```bash
ssh-keygen -q -t ed25519 -f /tmp/devpod-aks-ssh -N '' -C devpod-aks
export AKS_SSH_PUBLIC_KEY_FILE=/tmp/devpod-aks-ssh.pub
```

### Step 3: Provision The AKS Cluster

```bash
./hack/provision_aks.sh
```

What this does:

- creates the resource group if needed
- deploys the AKS cluster from `infra/aks/main.bicep`
- enables RBAC, OIDC, workload identity, and Azure CNI Overlay

Current default choices:

- region: `westus2` unless overridden
- cluster name: `devpod-aks` unless overridden
- node size: `Standard_D4s_v3`

### Step 4: Run The AKS Smoke Workspace

```bash
./hack/devpod_up_aks_smoke.sh
```

What this does:

- fetches kubeconfig into `/tmp/devpod-aks-kubeconfig`
- installs or reuses the `aks-kubernetes` provider
- points that provider at the AKS context
- uses namespace `devpod-workspaces`
- uses storage class `managed-csi`
- runs `devpod up` against `samples/aks-smoke`

If the command succeeds, you should see a message telling you that the
devcontainer was created and that you can SSH into the workspace.

### Step 5: Verify The Workspace

List the DevPod workspaces:

```bash
DEVPOD_HOME="${DEVPOD_HOME:-/tmp/devpod-aks-home}" devpod list
```

You should see `aks-smoke`.

Open a shell in the workspace:

```bash
DEVPOD_HOME="${DEVPOD_HOME:-/tmp/devpod-aks-home}" devpod ssh aks-smoke
```

Inside the workspace, verify the mount:

```bash
pwd
ls -la /workspaces/aks-smoke
exit
```

You can also verify directly from Kubernetes:

```bash
kubectl --kubeconfig /tmp/devpod-aks-kubeconfig -n devpod-workspaces get pods,pvc
```

Expected result:

- one pod in `Running`
- one PVC in `Bound`

### Cleanup

Delete the smoke workspace:

```bash
DEVPOD_HOME="${DEVPOD_HOME:-/tmp/devpod-aks-home}" devpod delete aks-smoke --force --ignore-not-found
```

Delete the AKS cluster:

```bash
az aks delete --resource-group "$AKS_RESOURCE_GROUP" --name "$AKS_NAME" --yes
```

Delete the temporary SSH key:

```bash
rm -f /tmp/devpod-aks-ssh /tmp/devpod-aks-ssh.pub
```

Optional: if you used the temporary DevPod home, remove it too:

```bash
rm -rf /tmp/devpod-aks-home /tmp/devpod-aks-kubeconfig
```

For the longer-form AKS notes, see [docs/aks-devpod-smoke.md](docs/aks-devpod-smoke.md).

## Development

Development and sample-image publishing details are in [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

## Registry Authentication

- `ManagedIdentity` is the default and recommended mode.
- `KeyVault` requires `KEYVAULT_URI`, `ACR_USERNAME_SECRET_NAME`, and `ACR_PASSWORD_SECRET_NAME`.
- `UsernamePassword` requires `ACR_USERNAME` and `ACR_PASSWORD`.

Managed Identity is the preferred path for ACR pulls when the image registry is Azure Container Registry.
