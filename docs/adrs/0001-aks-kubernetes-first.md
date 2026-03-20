# ADR 0001: Prefer AKS And DevPod Kubernetes Workflows Over Further ACI Provider Investment

- Status: Accepted
- Date: 2026-03-17

## Context

This repository currently implements a DevPod provider that drives Azure Container Instances (ACI) directly.

That approach has hit a structural mismatch with DevPod's runtime model:

- the ACI path depends on `exec` for agent injection and post-inject connectivity
- the latest handoff shows the flow can reach `done inject` but stalls when DevPod expects a clean long-lived STDIO transport for `helper ssh-server --stdio`
- the evidence collected in `docs/aci-e2e-handoff-2026-03-16.md` points to ACI exec behaving like an interactive terminal feature, not a documented raw byte tunnel
- continuing on ACI means carrying transport workarounds inside provider code for a path that is still not a reliable architectural fit

At the same time:

- DevPod already has a Kubernetes-oriented, non-machine model
- Azure Kubernetes Service (AKS) provides the primitives that match that model more naturally: pods, services, volumes, init containers, namespaces, and standard networking
- the repository should stay as DRY as possible and avoid custom code when upstream DevPod and standard Kubernetes assets are sufficient

## Decision

We will treat AKS plus DevPod's Kubernetes or non-machine workflow as the strategic target.

More specifically:

- stop treating ACI exec plus STDIO tunneling as the primary path forward
- stop adding net-new feature work whose only purpose is to make DevPod's machine-style provider model fit ACI
- prove the direct DevPod-on-AKS path first, before designing any replacement provider
- assume "no custom provider" is the default outcome unless the AKS spike exposes a concrete gap that cannot be solved with standard DevPod Kubernetes configuration, manifests, images, or thin helper scripts
- if a repo-owned integration is still needed later, keep it as thin and declarative as possible rather than rebuilding a full infrastructure provider

## Consequences

### Positive

- aligns the platform with DevPod's documented Kubernetes model instead of fighting it
- removes the need to rely on undocumented or weakly documented ACI exec transport behavior
- shifts the repo toward reusable Kubernetes assets, workspace images, and documentation instead of transport-heavy provider code
- reduces the chance that we maintain two different workspace lifecycle models for the same product goal

### Negative

- AKS has more cluster-level operational overhead than ACI
- some existing ACI-specific code and docs may eventually be retired, archived, or narrowed to historical context
- the team must now define cluster conventions for namespaces, storage, identity, image pulls, and network exposure

### Transitional

- the current ACI provider remains useful as a reference and fallback while the AKS path is proven
- new ACI work should be limited to maintenance, documentation, or short-lived experiments that directly inform the migration decision

## Alternatives Considered

### Continue investing in ACI exec as the transport

Rejected as the strategic path. The current evidence shows ongoing friction at exactly the layer where DevPod needs the most deterministic behavior.

### Redesign ACI around startup commands and a real SSH port

Kept as a tactical fallback, not the primary strategy. It is a better fit than ACI exec, but it still keeps the project centered on adapting DevPod to ACI instead of using the Kubernetes model DevPod already supports.

### Build a new AKS-specific provider immediately

Deferred. That would risk recreating orchestration and configuration logic that DevPod plus Kubernetes may already handle well enough.

## Decision Drivers

- architectural fit is more important than preserving the existing ACI investment
- transport reliability matters more than elegance of a no-port exec tunnel
- repo simplicity and reuse matter more than owning every layer
- future work should optimize for standard Kubernetes primitives and upstream DevPod behavior

## Follow-Up

The execution plan for this decision lives in `docs/aks-transition-plan.md`.

The detailed ACI evidence base remains in `docs/aci-e2e-handoff-2026-03-16.md` and should be referenced rather than copied into future docs.
