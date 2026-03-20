# AKS Transition Plan

## Goal

Deliver a DevPod workflow on Azure Kubernetes Service that avoids the ACI exec transport burden and keeps repo-owned code to the minimum necessary.

## Working Assumptions

- the strategic target is DevPod's Kubernetes or non-machine model on AKS
- the preferred outcome is to avoid building a new custom provider at all
- if extra repository logic is needed, it should be thin, declarative, and reusable
- the existing ACI provider stays in place until the AKS path is proven

## Guiding Rules

1. Prove the no-provider path first.
2. Prefer Kubernetes manifests, workspace images, and small scripts over new control-plane code.
3. Keep one source of truth for environment inputs, naming, and workspace bootstrap behavior.
4. Do not run ACI feature work and AKS productization in parallel unless it directly reduces migration risk.

## Success Criteria

The AKS path is good enough only when all of these are true:

- `devpod up` can create a usable workspace on AKS
- IDE or CLI attachment works through DevPod's supported Kubernetes path
- the workspace has a clear persistence story for user data
- image pulls, authentication, and cluster access are documented and repeatable
- teardown is predictable and does not leave orphaned cluster resources

## Phase 0: Freeze The Problem Statement

Purpose: stop drifting deeper into ACI-specific transport work before the replacement target is validated.

Tasks:

- treat `docs/aci-e2e-handoff-2026-03-16.md` as the evidence archive for why the pivot exists
- freeze new ACI exec transport work unless it is needed for a bounded experiment or maintenance fix
- define the acceptance checklist for the AKS path before implementation starts

Exit criteria:

- the team agrees that the next major spike is AKS-first, not ACI-first
- success is defined in terms of DevPod user outcomes, not provider internals

## Phase 1: Prove Direct DevPod-On-AKS

Purpose: answer the highest-value question first: do we need any custom provider at all?

Tasks:

- stand up a minimal AKS environment suitable for a single DevPod workspace flow
- validate DevPod against AKS using its Kubernetes or non-machine workflow without adding repo provider code
- identify the minimum required cluster assets:
  - namespace strategy
  - service account and RBAC
  - storage class and PVC pattern
  - image pull secret or workload identity pattern
  - service or ingress model, only if needed by the workflow
- record the exact gaps between a successful spike and a repeatable team workflow

Deliverables:

- a short runbook for the direct AKS flow
- a small set of reusable cluster manifests or templates, only if they are needed for the spike
- a gap list labeled as `docs gap`, `config gap`, `bootstrap gap`, or `product gap`

Exit criteria:

- one end-to-end DevPod workspace works on AKS without a repo-owned provider
- the remaining gaps are concrete and written down

## Phase 2: Productize The Thinnest Viable Layer

Purpose: make the direct AKS path repeatable without prematurely building a provider.

Tasks:

- move reusable Kubernetes assets into a single deploy area such as `deploy/aks/` or `k8s/`
- keep environment-specific values centralized instead of scattering them across docs, scripts, and manifests
- define one workspace image contract that can be reused across smoke tests and AKS runs
- add only the smallest helper scripts needed to apply manifests, validate prerequisites, or render a few variables
- document the operator path and the developer path separately so responsibilities stay clear

DRY constraints:

- one manifest or template set for shared cluster prerequisites
- one workspace bootstrap path for all supported AKS flows
- one place for required variables and default values
- no duplicate provider-style lifecycle logic in scripts if DevPod already handles it

Exit criteria:

- another engineer can reproduce the AKS workflow from repo docs and assets
- repo additions are mostly docs, manifests, and minimal glue

## Phase 3: Decide Whether Any Custom Integration Is Still Needed

Purpose: avoid inventing a provider because the old repo already has one.

Decision gate:

- if the remaining issues are documentation, cluster bootstrap, or static configuration, do not build a provider
- if the remaining issues are small workflow ergonomics, prefer a thin wrapper script or config generator
- only consider a new provider if DevPod's Kubernetes path cannot express a required behavior with acceptable reliability and operator effort

If a custom layer is still justified, constrain it to:

- stable inputs and outputs
- no custom transport protocol
- no duplication of Kubernetes lifecycle logic that already exists upstream
- a narrow responsibility such as config generation or policy validation

Exit criteria:

- a written follow-up decision says either `no provider`, `thin helper`, or `provider required`

## Phase 4: Retire Or Re-scope The ACI Implementation

Purpose: reduce confusion once the AKS path is credible.

Tasks:

- mark ACI docs as legacy, fallback, or archived once the AKS path replaces them
- decide whether the current provider becomes maintenance-only, experimental, or removed from the main product story
- keep only the ACI artifacts that still provide reference value for future troubleshooting or historical context

Exit criteria:

- the repo has one primary architecture story
- users are not asked to choose between overlapping strategic directions without guidance

## Immediate Next Actions

1. Write the AKS acceptance checklist before any new implementation work.
2. Run a direct DevPod-on-AKS spike with no new provider code.
3. Capture the smallest reusable manifest set that the spike proves necessary.
4. Reassess whether the repo needs anything beyond docs, manifests, and helper scripts.

## Repo Shape To Aim For

If the AKS path succeeds without a custom provider, the repository should trend toward:

- `docs/` for ADRs, runbooks, and migration notes
- `deploy/` or `k8s/` for reusable AKS manifests or overlays
- `samples/` for one workspace image or reference workload
- optional lightweight helper scripts under `hack/`

The current provider code should remain only as long as it still serves an explicit transitional purpose.
