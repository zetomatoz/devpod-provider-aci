# Development Guide

This repository currently targets a direct ACI workflow for published workspace images.

## Current Development Contract

- `WORKSPACE_IMAGE` is required for `create`
- Local-path and git workspaces are intentionally rejected
- Private VNet/subnet deployment is intentionally rejected
- The hello-world smoke test uses a published container image, not the sample folder as a DevPod source

## Repository Highlights

- `src/DevPod.Provider.ACI/`: provider implementation
- `src/DevPod.Provider.ACI.Tests/`: unit and integration-style tests
- `samples/dotnet-hello-world/`: sample ASP.NET app plus image build assets
- `tests/e2e/README.md`: manual smoke-test walkthrough
- `provider.yaml`: DevPod provider manifest

## Prerequisites

- .NET 8 SDK
- Azure CLI for interactive Azure auth, or service principal credentials
- DevPod CLI
- Docker only if you want to build/publish the sample image yourself

## Build

### Linux/macOS

```bash
./hack/build.sh
```

### Windows

```bash
./hack/build.ps1
```

### Manual Build

```bash
dotnet build DevPod.Provider.ACI.sln
dotnet publish src/DevPod.Provider.ACI/DevPod.Provider.ACI.csproj -c Release -o dist/
```

## Test

```bash
dotnet test src/DevPod.Provider.ACI.Tests/DevPod.Provider.ACI.Tests.csproj
```

## Local Provider Install

```bash
./hack/build.sh
devpod provider add ./dist/provider-local.yaml --name aci-local
```

## Manual Smoke Test

Use a published image source:

```bash
export AZURE_SUBSCRIPTION_ID="your-subscription-id"
export AZURE_RESOURCE_GROUP="devpod-test-rg"
export AZURE_REGION="eastus"

devpod up ghcr.io/zetomatoz/devpod-provider-aci-hello-world:latest \
  --provider aci-local \
  --workspace aci-hello \
  --ide none
```

Then validate the running app:

```bash
devpod ssh aci-hello
curl -fsS http://127.0.0.1:8080/health
curl -fsS http://127.0.0.1:8080/
exit
```

## Sample Image Publishing

The sample app has a Dockerfile at `samples/dotnet-hello-world/Dockerfile`.

Build it locally:

```bash
docker build -t ghcr.io/<your-org>/devpod-provider-aci-hello-world:dev samples/dotnet-hello-world
```

The repository also includes a GitHub Actions workflow that can publish the sample image to GHCR.

## Debug Logging

```bash
export DEVPOD_DEBUG=true
```

## Architecture

For architecture notes, see [ARCHITECTURE.md](./ARCHITECTURE.md) and [command-execution-flow.md](./command-execution-flow.md).
