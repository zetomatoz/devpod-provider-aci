# .NET Hello World Sample

This sample is a richer follow-up workspace for the AKS path.

Use it when:

- `samples/aks-smoke` already works
- you want an HTTP endpoint to validate inside the workspace
- you want an optional sample image for demos or image-based experiments

## Local-Path Workspace

After configuring the `devpod-aks` provider entry, you can run:

```bash
DEVPOD_HOME="${DEVPOD_HOME:-/tmp/devpod-aks-home}" \
devpod up ./samples/dotnet-hello-world \
  --provider devpod-aks \
  --id dotnet-hello \
  --ide none
```

Then verify the app:

```bash
DEVPOD_HOME="${DEVPOD_HOME:-/tmp/devpod-aks-home}" \
devpod ssh dotnet-hello --command 'curl -fsS http://127.0.0.1:8080/health && curl -fsS http://127.0.0.1:8080/'
```

## Build A Sample Image

Build the image locally:

```bash
docker buildx build \
  --platform linux/amd64 \
  -f samples/dotnet-hello-world/Dockerfile \
  -t ghcr.io/<repo-owner>/devpod-aks-hello-world:latest \
  --load .
```

Push the image:

```bash
docker buildx build \
  --platform linux/amd64 \
  -f samples/dotnet-hello-world/Dockerfile \
  -t ghcr.io/<repo-owner>/devpod-aks-hello-world:latest \
  --push .
```

`linux/amd64` matches the default AKS node pool architecture used by this repo's
smoke environment.
