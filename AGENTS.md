# AGENTS.md

This file provides guidance to Codex when working in this repository.

## Project Overview

This repository is an AKS-first DevPod blueprint. It focuses on:

- provisioning Azure Kubernetes Service with reusable infrastructure
- configuring DevPod's built-in `kubernetes` provider for AKS
- keeping smoke-test workspaces and helper scripts small and repeatable

The mainline repository no longer carries the retired Azure Container Instances
provider implementation. Historical ACI notes live only under `docs/archive/aci/`.

Recommended repository name: `devpod-aks`.

## Repository State

The supported story is now:

- `infra/aks/` defines the cluster shape
- `hack/provision_aks.sh` provisions AKS
- `hack/devpod_up_aks_smoke.sh` wires DevPod to the cluster
- `samples/aks-smoke/` is the first validation target
- `samples/dotnet-hello-world/` is an optional richer sample

## Development Setup

Common local tools:

- Azure CLI
- `kubectl`
- DevPod CLI
- .NET 8 SDK for the sample app
- Python 3 for lightweight local validation helpers

## Architecture Notes

- Prefer DevPod's first-party Kubernetes workflow over repo-owned control-plane
  code.
- Keep repo logic thin and declarative: Bicep, shell helpers, docs, and sample
  workspaces.
- Avoid rebuilding lifecycle logic that DevPod and Kubernetes already provide.

## Repository Hygiene

- Never commit machine-specific absolute filesystem paths such as `/Users/...`
  into repository files.
- Prefer repo-relative paths in documentation and generic paths in examples so
  the repository stays portable.
- Keep ACI material in `docs/archive/aci/` only; do not reintroduce it into the
  main product story unless the user explicitly asks for historical context.
