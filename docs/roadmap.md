# AKS Roadmap

This repository has completed the strategic pivot away from ACI. The next work
should deepen the AKS path rather than reintroduce provider code.

## Near-Term Priorities

1. Add reusable AKS bootstrap assets beyond the cluster itself:
   namespace, RBAC, and optional policy defaults.
2. Document the recommended identity story for private image pulls and Azure
   service access from workspaces.
3. Add a richer end-to-end validation path based on
   `samples/dotnet-hello-world/`.
4. Keep contributor validation lightweight and repo-local.

## Open Questions

- Do we want a checked-in namespace and RBAC manifest set under `deploy/` or
  `k8s/`?
- Should the repository publish a sample workspace image for image-based AKS
  workflows as a convenience?
- Which storage class and namespace defaults should become team standards?

## Non-Goals

- rebuilding a custom DevPod provider
- resurrecting direct ACI transport work in the main product story
- duplicating Kubernetes lifecycle behavior already handled by DevPod
