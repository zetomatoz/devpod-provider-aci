# End-to-End Smoke Test: Hello World Sample on ACI

This guide walks through a minimal end-to-end test of the DevPod ACI provider by launching the `samples/dotnet-hello-world` workspace on Azure Container Instances. The flow assumes you are on macOS, have an Azure subscription, and start with no Azure resources provisioned.

## 1. Prerequisites

- macOS with Bash and Homebrew.
- Azure CLI logged in with rights to create resource groups and ACI container groups.
- DevPod CLI and the .NET 8 SDK (for building the provider binary).

```bash
# Azure CLI (if not already installed)
brew update && brew install azure-cli

# DevPod CLI (install script; see https://devpod.sh/docs/getting-started/install for alternatives)
curl -fsSL https://get.devpod.sh | sh

# .NET 8 SDK
brew install --cask dotnet-sdk
```

Authenticate and select the subscription you will use:

```bash
az login                      # Opens browser for interactive login
az account set --subscription "<SUBSCRIPTION_ID>"
```

Export the required environment variables so the provider can read them non-interactively:

```bash
export AZURE_SUBSCRIPTION_ID="<SUBSCRIPTION_ID>"
export AZURE_RESOURCE_GROUP="devpod-aci-e2e"
export AZURE_REGION="westus2"
```

## 2. Create the Resource Group

The provider expects the resource group to exist. Create it once before running the test:

```bash
az group create \
  --name "$AZURE_RESOURCE_GROUP" \
  --location "$AZURE_REGION"
```

## 3. Build the Provider Locally

From the repository root:

```bash
./hack/build.sh
```

This script publishes the provider binaries into `./dist/` and renders `provider-local.yaml` that references the freshly built binaries.

## 4. Register the Local Provider with DevPod

- Re-use the releasable assets in `./dist/`. 
- `provider-local.yaml` resolves binaries from your local workspace, so no GitHub release is required.

```bash
devpod provider add ./dist/provider-local.yaml --name aci-local
```

You can confirm registration with `devpod provider list`.

## 5. Launch the Hello World Sample

The provider reads configuration from environment variables each time DevPod invokes it. Before running `devpod up`, make sure the following baseline variables are exported in your shell (or provided as DevPod provider options):

```bash
export AZURE_SUBSCRIPTION_ID="<SUBSCRIPTION_ID>"          # required
export AZURE_RESOURCE_GROUP="${AZURE_RESOURCE_GROUP:-devpod-aci-e2e}"  # defaults to devpod-aci-rg if omitted
export AZURE_REGION="${AZURE_REGION:-westus2}"            # defaults to eastus if omitted
```

If you rely on an Azure Service Principal instead of `az login`, also export `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, and `AZURE_CLIENT_SECRET`. All other provider settings fall back to the built-in defaults:

- Image: `mcr.microsoft.com/devcontainers/base:ubuntu`
- CPU / Memory: `2 vCPU`, `4 GiB`
- GPU: disabled
- Restart policy: `Never`
- SSH port 22 exposed

### 5.1 Machine Provider Mode (default)

In machine mode DevPod manages the `MACHINE_ID` and other machine metadata automatically. Ensure `ACI_PROVIDER_SETUP` is unset or set to `Machine`:

```bash
unset ACI_PROVIDER_SETUP  # or: export ACI_PROVIDER_SETUP=Machine
devpod up ./samples/dotnet-hello-world \
  --provider aci-local \
  --workspace aci-hello \
  --ide vscode
```

The CLI uploads the DevPod agent, provisions an ACI container group named after the machine (for example `devpod-aci-hello`), and mounts the sample repository snapshot inside the container.

### 5.2 Workspace Provider Mode (no machine record)

If you prefer to address container groups directly without creating a DevPod machine record, switch the provider into workspace mode. Provide a deterministic container group name so repeated `devpod up` calls reuse the same group:

```bash
export ACI_PROVIDER_SETUP=Workspace
export ACI_CONTAINER_GROUP_NAME="aci-hello-ws"   # optional override; otherwise derived from WORKSPACE_UID
devpod up ./samples/dotnet-hello-world \
  --provider aci-local \
  --workspace aci-hello-ws \
  --ide vscode
unset ACI_PROVIDER_SETUP
unset ACI_CONTAINER_GROUP_NAME
```

In workspace mode the provider uses the workspace UID or the supplied `ACI_CONTAINER_GROUP_NAME` to build the container group name while keeping every other default identical to machine mode.

The provisioning flow typically completes within a couple of minutes in either mode.

## 6. Validate the Workspace

Once the workspace is reported as `Ready`, open a shell inside it and run the sample:

```bash
devpod ssh aci-hello         # Attach to the workspace shell
dotnet run                   # Inside the container: should print "Hello, World!"
exit                         # Leave the container shell
```

You can also run `devpod status aci-hello` from macOS to verify the agent is healthy.

## 7. Clean Up

Tear down the workspace container and (optionally) the resource group:

```bash
devpod delete aci-hello      # Removes the ACI container group

# Optional: only if you created the RG exclusively for this test
az group delete --name "$AZURE_RESOURCE_GROUP" --yes --no-wait
```

## Troubleshooting Tips

- If `devpod up` fails with authentication errors, re-run `az login` and ensure `AZURE_SUBSCRIPTION_ID` matches the active subscription.
- Container image pulls use `mcr.microsoft.com/devcontainers/dotnet:8.0`, so no Azure Container Registry setup is required for this smoke test.
- For verbose provider logs, set `DEVPOD_DEBUG=true` before running `devpod up`.

## Appendix: Cutting a Release

When you are ready to publish a new provider release, use the helper script to build binaries, compute checksums, and render the release manifest (`dist/provider.yaml`) that points to GitHub-hosted binaries:

```bash
./hack/release.sh 0.2.0
```

The script runs the cross-platform build, writes fresh `.sha256` files into `dist/`, and emits the final `dist/provider.yaml` with the `${VERSION}` and `${CHECKSUM_*}` placeholders substituted. When it finishes you will see messages such as:

```
==> Release manifest written to /path/to/dist/provider.yaml
==> Upload binaries from /path/to/dist along with provider.yaml to the GitHub release
```

That means the release bundle is ready. To publish it:

1. Verify `dist/provider.yaml` contains the expected `version: v0.2.0` and URLs that match the release tag.
2. Collect the binaries and checksums produced in `dist/` (Linux, macOS, Windows, plus their `.sha256` files).
3. Upload `provider.yaml` and every binary/checksum pair to the matching GitHub release/tag.
4. Optionally delete the intermediary per-runtime folders in `dist/` once the release is live.

To let the script upload assets automatically with the GitHub CLI (must be installed and authenticated via `gh auth login`), add `--publish`:

```bash
./hack/release.sh --publish 0.2.0
```

With `--publish`, the script uploads the generated files to the `v0.2.0` release—creating it if it does not already exist.
