# DevPod on AKS

This repository is now an AKS-first blueprint for running DevPod workspaces on
Azure Kubernetes Service by using DevPod's built-in `kubernetes` provider. The
old ACI provider implementation has been retired from the mainline repository.

Repository name: `devpod-provider-aks`.

The older `devpod-provider-aci` name now refers only to the retired ACI
prototype period.

## What This Repo Contains

- `infra/aks/`: AKS infrastructure definition in Bicep
- `hack/provision_aks.sh`: creates or updates the AKS cluster
- `hack/devpod_up_aks_smoke.sh`: configures DevPod against AKS and runs the
  smoke workspace
- `samples/aks-smoke/`: smallest supported workspace for first validation
- `samples/dotnet-hello-world/`: richer sample app for follow-up validation
- `docs/`: architecture notes, contributor guidance, runbooks, and roadmap

## Quick Start

Set the required Azure variables:

```bash
export AZURE_SUBSCRIPTION_ID="<subscription-id>"
export AZURE_REGION="westus2"
export AKS_RESOURCE_GROUP="devpod-aks-rg"
export AKS_NAME="devpod-aks"
```

Create a temporary SSH key for the AKS node pool:

```bash
ssh-keygen -q -t ed25519 -f /tmp/devpod-aks-ssh -N '' -C devpod-aks
export AKS_SSH_PUBLIC_KEY_FILE=/tmp/devpod-aks-ssh.pub
```

Provision the cluster:

```bash
./hack/provision_aks.sh
```

Run the first smoke workspace:

```bash
./hack/devpod_up_aks_smoke.sh
```

Verify the workspace:

```bash
DEVPOD_HOME="${DEVPOD_HOME:-/tmp/devpod-aks-home}" devpod list
DEVPOD_HOME="${DEVPOD_HOME:-/tmp/devpod-aks-home}" devpod ssh aks-smoke
kubectl --kubeconfig /tmp/devpod-aks-kubeconfig -n devpod-workspaces get pods,pvc
```

The detailed walkthrough lives in [docs/runbooks/aks-smoke.md](docs/runbooks/aks-smoke.md).

## Repo Layout

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md): repo architecture and control
  flow
- [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md): contributor guide and validation
  commands
- [docs/roadmap.md](docs/roadmap.md): next improvements for the AKS path
- [docs/adrs/0001-aks-kubernetes-first.md](docs/adrs/0001-aks-kubernetes-first.md):
  architectural decision record for the pivot
- [samples/aks-smoke/README.md](samples/aks-smoke/README.md): smoke workspace
  notes
- [samples/dotnet-hello-world/README.md](samples/dotnet-hello-world/README.md):
  optional richer sample

## Operating Model

- The primary workflow is `DevPod CLI -> kubernetes provider -> AKS`.
- The repository owns only the AKS bootstrap assets and helper scripts.
- No custom DevPod provider is shipped from this repo anymore.

## Archived ACI Material

Historical ACI research is still available under `docs/archive/aci/` for
context, but it is no longer part of the supported path.
