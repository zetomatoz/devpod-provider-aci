# AKS Smoke Workspace

This directory is the first workspace to use when validating the AKS path.

Why it exists:

- it keeps the initial `devpod up` fast
- it avoids syncing unrelated repository artifacts
- it isolates cluster and provider validation from application complexity

Run it through `./hack/devpod_up_aks_smoke.sh` before moving to larger samples.
