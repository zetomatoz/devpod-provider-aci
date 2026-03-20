# AKS DevPod Smoke Runbook

Snapshot date: 2026-03-18.

This runbook captures the AKS-backed DevPod flow that was proven in this repository.

## What Was Proven

The following path completed successfully:

- provision AKS from `infra/aks/main.bicep`
- fetch kubeconfig for that cluster
- configure DevPod's first-party `kubernetes` provider against the AKS context
- run `devpod up` successfully against `samples/aks-smoke`

## Why `samples/aks-smoke` Exists

The repository root currently contains large local build outputs under `dist/`,
`src/DevPod.Provider.ACI/bin`, and `src/DevPod.Provider.ACI/obj`.

Using the full repository as the first local-path workspace causes DevPod to sync
hundreds of megabytes of local artifacts into the pod. The `samples/aks-smoke`
workspace avoids that and gives a fast, repeatable proof that the AKS path works.

## Prerequisites

- Azure CLI logged into the target subscription
- `AZURE_SUBSCRIPTION_ID` set
- `AZURE_REGION` set, or `AKS_LOCATION`
- `AKS_RESOURCE_GROUP` set
- DevPod CLI installed
- An SSH public key provided through `AKS_SSH_PUBLIC_KEY` or `AKS_SSH_PUBLIC_KEY_FILE`

## Provision AKS

Create a temporary SSH key if you do not already have one available:

```bash
ssh-keygen -q -t ed25519 -f /tmp/devpod-aks-ssh -N '' -C devpod-aks
```

Deploy the cluster:

```bash
AKS_RESOURCE_GROUP=devpod-aks-rg \
AKS_NAME=devpod-aks \
AKS_SSH_PUBLIC_KEY_FILE=/tmp/devpod-aks-ssh.pub \
./hack/provision_aks.sh
```

The current default node size is `Standard_D4s_v3`. That default was chosen after
the original `Standard_D4ds_v5` attempt failed in `westus2` with a zero-quota VM family.

The AKS helpers intentionally do not fall back to `AZURE_RESOURCE_GROUP`. This
repository's direct ACI flow often uses a different resource group, and reusing
that variable for AKS is an easy way to fetch credentials from the wrong place.

## Run The Smoke Workspace

```bash
AKS_RESOURCE_GROUP=devpod-aks-rg \
AKS_NAME=devpod-aks \
./hack/devpod_up_aks_smoke.sh
```

That script will:

- fetch kubeconfig into `/tmp/devpod-aks-kubeconfig`
- use `/tmp/devpod-aks-home` as the DevPod home
- install or reuse the `aks-kubernetes` provider
- pin the provider to:
  - context `devpod-aks`
  - namespace `devpod-workspaces`
  - storage class `managed-csi`
- run `devpod up` against `samples/aks-smoke`

## Verify The Workspace

List workspaces:

```bash
DEVPOD_HOME=/tmp/devpod-aks-home devpod list
```

Run a command inside the smoke workspace:

```bash
DEVPOD_HOME=/tmp/devpod-aks-home devpod ssh aks-smoke --command 'pwd && ls -la /workspaces/aks-smoke'
```

## Cleanup

Delete the smoke workspace:

```bash
DEVPOD_HOME=/tmp/devpod-aks-home devpod delete aks-smoke --force --ignore-not-found
```

Delete the AKS cluster:

```bash
az aks delete --resource-group devpod-aks-rg --name devpod-aks --yes
```

## Next Step

Keep using `samples/aks-smoke` as the first validation target. Once that remains
stable, decide whether to:

- clean local build outputs before using the full repository as a local-path workspace
- teach the team a more reliable ignore or packaging flow for large local artifacts
- keep the AKS path focused on smaller, purpose-built workspace sources
