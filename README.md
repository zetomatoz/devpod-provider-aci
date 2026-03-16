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

## Development

Development and sample-image publishing details are in [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

## Registry Authentication

- `ManagedIdentity` is the default and recommended mode.
- `KeyVault` requires `KEYVAULT_URI`, `ACR_USERNAME_SECRET_NAME`, and `ACR_PASSWORD_SECRET_NAME`.
- `UsernamePassword` requires `ACR_USERNAME` and `ACR_PASSWORD`.

Managed Identity is the preferred path for ACR pulls when the image registry is Azure Container Registry.
