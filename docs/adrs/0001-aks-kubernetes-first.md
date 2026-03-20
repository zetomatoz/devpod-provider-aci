# ADR 0001: Make AKS The Primary And Only Supported Path In This Repository

- Status: Accepted and implemented
- Date: 2026-03-17
- Updated: 2026-03-19

## Context

The repository originally explored a custom Azure Container Instances path for
DevPod. That work proved useful for learning, but it did not remain the right
long-term fit.

By 2026-03-18, the direct AKS smoke path had already been proven with:

- AKS provisioned from `infra/aks/main.bicep`
- DevPod's built-in `kubernetes` provider
- a successful `devpod up` run against `samples/aks-smoke`

At that point, keeping the ACI provider code in the mainline repository created
two conflicting stories:

- an old custom-provider path we no longer wanted to invest in
- a simpler AKS path that matched DevPod's Kubernetes model better

## Decision

We will standardize this repository around AKS and DevPod's built-in
`kubernetes` provider.

More specifically:

- the repository no longer ships a custom DevPod provider
- ACI-specific implementation code is removed from the mainline repo
- historical ACI notes are kept only under `docs/archive/aci/`
- repo naming, docs, scripts, and samples all point to the AKS-first workflow
- future work should add AKS bootstrap assets, docs, and samples rather than a
  replacement provider unless a new decision record explicitly says otherwise

## Consequences

### Positive

- one clear architecture story for users and contributors
- less repo-owned control-plane code
- closer alignment with upstream DevPod and Kubernetes behavior
- lower maintenance burden than carrying the old provider stack

### Negative

- the repo now assumes AKS operational overhead instead of ACI simplicity
- users who still care about the old ACI experiments must rely on archived notes
  or Git history

## Follow-Up

- use [docs/roadmap.md](../roadmap.md) for next AKS improvements
- keep archived ACI material read-only unless explicitly needed for historical
  reference
