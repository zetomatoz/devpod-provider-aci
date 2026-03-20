# Development Guide

This repository now supports an AKS-first workflow only.

## Prerequisites

- Azure CLI
- `kubectl`
- DevPod CLI
- .NET 8 SDK
- Python 3

## Repository Highlights

- `infra/aks/`: AKS cluster definition
- `hack/provision_aks.sh`: AKS provisioning helper
- `hack/devpod_up_aks_smoke.sh`: DevPod smoke helper
- `samples/aks-smoke/`: first smoke workspace
- `samples/dotnet-hello-world/`: richer sample app and optional image source

## Recommended Validation Commands

Validate the shell helpers:

```bash
bash -n hack/*.sh
```

Validate the checked-in JSON files:

```bash
python3 -m json.tool .devcontainer/devcontainer.json >/dev/null
python3 -m json.tool infra/aks/main.json >/dev/null
python3 -m json.tool samples/aks-smoke/.devcontainer/devcontainer.json >/dev/null
python3 -m json.tool samples/dotnet-hello-world/.devcontainer/devcontainer.json >/dev/null
```

Build the richer sample app:

```bash
dotnet restore --locked-mode samples/dotnet-hello-world/HelloWorld.csproj
dotnet build --no-restore --configuration Release samples/dotnet-hello-world/HelloWorld.csproj
```

## Local Smoke Workflow

Use the smallest workspace first:

```bash
export AZURE_SUBSCRIPTION_ID="<subscription-id>"
export AZURE_REGION="westus2"
export AKS_RESOURCE_GROUP="devpod-aks-rg"
export AKS_NAME="devpod-aks"
export AKS_SSH_PUBLIC_KEY_FILE="/path/to/key.pub"

./hack/provision_aks.sh
./hack/devpod_up_aks_smoke.sh
```

Detailed operator steps are in [runbooks/aks-smoke.md](./runbooks/aks-smoke.md).

## Sample App

`samples/dotnet-hello-world/` is no longer tied to a custom provider. Use it as:

- a local-path DevPod workspace after the AKS smoke run succeeds
- an optional container image for follow-up validation

Image publishing notes live in
[samples/dotnet-hello-world/README.md](../samples/dotnet-hello-world/README.md).

## Documentation Rules

- Keep the primary story AKS-first.
- Put historical ACI notes only under `docs/archive/aci/`.
- Prefer repo-relative paths and generic examples over local absolute paths.
