# AKS DevPod Smoke Runbook

Snapshot date: 2026-03-19.

This runbook documents the AKS-backed DevPod path that is now the primary
workflow in this repository.

## What This Proves

The supported smoke path is:

- provision AKS from `infra/aks/main.bicep`
- fetch kubeconfig for that cluster
- configure DevPod's built-in `kubernetes` provider against the AKS context
- run `devpod up` successfully against `samples/aks-smoke`

## Why `samples/aks-smoke` Comes First

The repository root can accumulate local build output and other contributor
artifacts. `samples/aks-smoke` stays intentionally tiny so the first `devpod up`
exercise validates the cluster and provider path without syncing unrelated repo
content into the workspace.

## Prerequisites

- Azure CLI logged into the target subscription
- `AZURE_SUBSCRIPTION_ID` set
- `AZURE_REGION` set, or `AKS_LOCATION`
- `AKS_RESOURCE_GROUP` set
- DevPod CLI installed
- `kubectl` installed
- an SSH public key available through `AKS_SSH_PUBLIC_KEY` or
  `AKS_SSH_PUBLIC_KEY_FILE`

## 1. Create An SSH Key

Create a temporary key if needed:

```bash
ssh-keygen -q -t ed25519 -f /tmp/devpod-aks-ssh -N '' -C devpod-aks
export AKS_SSH_PUBLIC_KEY_FILE=/tmp/devpod-aks-ssh.pub
```

## 2. Provision AKS

```bash
AKS_RESOURCE_GROUP=devpod-aks-rg \
AKS_NAME=devpod-aks \
AKS_SSH_PUBLIC_KEY_FILE=/tmp/devpod-aks-ssh.pub \
./hack/provision_aks.sh
```

Current defaults:

- region from `AKS_LOCATION` or `AZURE_REGION`
- cluster name `devpod-aks`
- node size `Standard_D4s_v3`
- workload identity enabled

## 3. Run The Smoke Workspace

```bash
AKS_RESOURCE_GROUP=devpod-aks-rg \
AKS_NAME=devpod-aks \
./hack/devpod_up_aks_smoke.sh
```

The helper script will:

- fetch kubeconfig into `/tmp/devpod-aks-kubeconfig`
- use `/tmp/devpod-aks-home` unless `DEVPOD_HOME` is already set
- install or reuse a DevPod provider entry named `devpod-aks`
- target namespace `devpod-workspaces`
- target storage class `managed-csi`
- run `devpod up` against `samples/aks-smoke`

## 4. Verify The Workspace

List workspaces:

```bash
DEVPOD_HOME="${DEVPOD_HOME:-/tmp/devpod-aks-home}" devpod list
```

Open a shell:

```bash
DEVPOD_HOME="${DEVPOD_HOME:-/tmp/devpod-aks-home}" devpod ssh aks-smoke
```

Inside the workspace, verify the mount:

```bash
pwd
ls -la /workspaces/aks-smoke
exit
```

Verify directly from Kubernetes:

```bash
kubectl --kubeconfig /tmp/devpod-aks-kubeconfig -n devpod-workspaces get pods,pvc
```

Expected result:

- one pod in `Running`
- one PVC in `Bound`

## 5. Cleanup

Delete the smoke workspace:

```bash
DEVPOD_HOME="${DEVPOD_HOME:-/tmp/devpod-aks-home}" devpod delete aks-smoke --force --ignore-not-found
```

Delete the cluster:

```bash
az aks delete --resource-group "$AKS_RESOURCE_GROUP" --name "$AKS_NAME" --yes
```

Delete the temporary local files:

```bash
rm -f /tmp/devpod-aks-ssh /tmp/devpod-aks-ssh.pub
rm -rf /tmp/devpod-aks-home /tmp/devpod-aks-kubeconfig
```
