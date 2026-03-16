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
- Python 3 for local manifest rendering
- Docker only if you want to build/publish the sample image yourself

If the subscription has not used ACI before, register the resource provider once:

```bash
az provider register --namespace Microsoft.ContainerInstance
```

## Build

There are two different build paths in this repository:

- `dotnet build` or `dotnet publish`: compile the provider binary for normal development work
- `./hack/build.sh`: package the provider for local DevPod usage by building binaries, generating checksums, and rendering a concrete provider manifest

### Compile Only

Use this when you only want to confirm the code builds:

```bash
dotnet build DevPod.Provider.ACI.sln
```

Use this when you want a single published binary for inspection:

```bash
dotnet publish src/DevPod.Provider.ACI/DevPod.Provider.ACI.csproj -c Release -o dist/
```

Those commands do not render `dist/provider-local.yaml`, so they are not enough on their own for `devpod provider add`.

### Local DevPod Packaging

```bash
./hack/build.sh
```

`./hack/build.sh` is the packaging script for local end-to-end testing. It:

- publishes self-contained binaries for each supported platform into `dist/`
- generates SHA256 checksum files for those binaries
- exports the paths and checksums as environment variables
- calls `hack/render_provider.py`
- writes `dist/provider-local.yaml`

### How `provider.yaml` Becomes `dist/provider-local.yaml`

The source manifest at [provider.yaml](../provider.yaml) contains placeholders such as:

- `${BINARY_LINUX_AMD64}`
- `${CHECKSUM_LINUX_AMD64}`

The script [hack/render_provider.py](../hack/render_provider.py) reads `provider.yaml` as a template and substitutes those placeholders using environment variables prepared by `./hack/build.sh`.

That is why the local install flow must use `./hack/build.sh` rather than plain `dotnet publish`.

## Test

```bash
dotnet test src/DevPod.Provider.ACI.Tests/DevPod.Provider.ACI.Tests.csproj
```

## Local Provider Install

```bash
./hack/build.sh
devpod provider add ./dist/provider-local.yaml --name aci-local
```

## Recommended Local Verification Order

```bash
dotnet test src/DevPod.Provider.ACI.Tests/DevPod.Provider.ACI.Tests.csproj
dotnet build DevPod.Provider.ACI.sln
./hack/build.sh
devpod provider add ./dist/provider-local.yaml --name aci-local
```

## Manual Smoke Test

Use a published image source. Do not use the sample folder itself as a DevPod workspace source.

```bash
export AZURE_SUBSCRIPTION_ID="your-subscription-id"
export AZURE_RESOURCE_GROUP="devpod-test-rg"
export AZURE_REGION="eastus"

devpod up ghcr.io/<your-org>/devpod-provider-aci-hello-world:latest \
  --id aci-hello \
  --provider aci-local \
  --provider-option WORKSPACE_IMAGE=ghcr.io/<your-org>/devpod-provider-aci-hello-world:latest \
  --ide none
```

Then validate both agent injection and the sample app:

```bash
devpod ssh aci-hello
ls -l /tmp/devpod
curl -fsS http://127.0.0.1:8080/health
curl -fsS http://127.0.0.1:8080/
exit
```

If `/tmp/devpod` exists and the HTTP checks succeed, the core create-plus-exec workflow is working.

## Sample Image Publishing

The sample app has a Dockerfile at `samples/dotnet-hello-world/Dockerfile`.

Build it locally:

```bash
docker build -t ghcr.io/<your-org>/devpod-provider-aci-hello-world:latest samples/dotnet-hello-world
```

The repository also includes a GitHub Actions workflow that can publish the sample image to GHCR.

That workflow publishes:

- `ghcr.io/<repo-owner>/devpod-provider-aci-hello-world:latest`
- `ghcr.io/<repo-owner>/devpod-provider-aci-hello-world:sha-<short-commit>`

So if you are using the workflow-produced sample image, `HELLO_WORLD_IMAGE` should point at the `:latest` tag for the repository owner that published it.

## Debug Logging

```bash
export DEVPOD_DEBUG=true
```

## Architecture

For architecture notes, see [ARCHITECTURE.md](./ARCHITECTURE.md) and [command-execution-flow.md](./command-execution-flow.md).
