# End-to-End Smoke Test: Hello World Image on ACI

This guide validates the current provider contract: a published image workspace running directly on Azure Container Instances.

Important constraints for this release:

- Use a published image source such as `ghcr.io/<repo-owner>/devpod-provider-aci-hello-world:latest`
- Do not use `devpod up ./samples/dotnet-hello-world`
- Do not use git or local-path sources
- Do not set `ACI_VNET_NAME` or `ACI_SUBNET_NAME`

## 1. Prerequisites

- Azure CLI authenticated with access to create ACI container groups and resource groups
- DevPod CLI
- .NET 8 SDK for building the provider binary
- Python 3 for rendering the local provider manifest

Authenticate and select the subscription:

```bash
az login
az account set --subscription "<SUBSCRIPTION_ID>"
az provider register --namespace Microsoft.ContainerInstance
```

## 2. Make Sure the Hello World Image Exists

This guide assumes `HELLO_WORLD_IMAGE` points to a container image that already exists in a registry accessible from Azure Container Instances.

You have two options:

- Use a published sample image tag if you already have one available.
- Build and push your own image first by following [samples/dotnet-hello-world/README.md](../../samples/dotnet-hello-world/README.md), then set `HELLO_WORLD_IMAGE` to that pushed image reference.

If you are using the repository GitHub Actions workflow, the published image name is:

```bash
ghcr.io/<repo-owner>/devpod-provider-aci-hello-world:latest
```

If the image does not exist yet, `devpod up` will fail during the ACI image pull step.

## 3. Configure Persistent Environment Variables

If you do not want to re-export variables in every new terminal session, add them to your shell startup file once.

For `zsh`:

```bash
cat <<'EOF' >> ~/.zshrc
export AZURE_SUBSCRIPTION_ID="<SUBSCRIPTION_ID>"
export AZURE_RESOURCE_GROUP="devpod-aci-e2e"
export AZURE_REGION="westus2"
export HELLO_WORLD_IMAGE="ghcr.io/<repo-owner>/devpod-provider-aci-hello-world:latest"
EOF

source ~/.zshrc
```

If you prefer not to keep these globally in your shell profile, you can export them manually in the current terminal instead:

```bash
export AZURE_SUBSCRIPTION_ID="<SUBSCRIPTION_ID>"
export AZURE_RESOURCE_GROUP="devpod-aci-e2e"
export AZURE_REGION="westus2"
export HELLO_WORLD_IMAGE="ghcr.io/<repo-owner>/devpod-provider-aci-hello-world:latest"
```

The provider will create the resource group automatically if it does not already exist.

## 4. Build and Register the Provider

Use the local packaging script, not plain `dotnet publish`.

```bash
./hack/build.sh
devpod provider add ./dist/provider-local.yaml --name aci-local
```

Why this step matters:

- `dotnet build` compiles the provider code
- `./hack/build.sh` packages the provider for DevPod
- `./hack/build.sh` calls `hack/render_provider.py`
- `hack/render_provider.py` transforms [provider.yaml](../../provider.yaml) into `dist/provider-local.yaml` by substituting binary paths and checksums

If `dist/provider-local.yaml` does not exist, DevPod has nothing concrete to install locally.

Optional compile-only checks before packaging:

```bash
dotnet test src/DevPod.Provider.ACI.Tests/DevPod.Provider.ACI.Tests.csproj
dotnet build DevPod.Provider.ACI.sln
```

## 5. Launch the Workspace

Use the published image as the DevPod source and also pass it through as a provider option so the provider `create` hook can provision that exact image in ACI:

```bash
devpod up "$HELLO_WORLD_IMAGE" \
  --id aci-hello \
  --provider aci-local \
  --provider-option WORKSPACE_IMAGE="$HELLO_WORLD_IMAGE" \
  --ide none
```

Expected behavior:

- DevPod invokes `create`
- DevPod uses the positional image source to define the workspace image-based source
- `--provider-option WORKSPACE_IMAGE="$HELLO_WORLD_IMAGE"` passes the same image to the provider `create` hook
- The provider provisions an ACI container group from `WORKSPACE_IMAGE`
- DevPod injects the agent using the provider `command` hook and the ACI exec WebSocket
- The workspace reaches a ready state

## 6. Validate the Running App

Open a shell in the workspace and hit the sample app:

```bash
devpod ssh aci-hello
ls -l /tmp/devpod
curl -fsS http://127.0.0.1:8080/health
curl -fsS http://127.0.0.1:8080/
exit
```

The checks mean:

- `/tmp/devpod` exists: DevPod agent injection through the provider `command` hook worked
- `/health` returns JSON with `status` equal to `Healthy`: the sample app is running
- `/` returns the sample app response: HTTP traffic works inside the workspace

You can also check provider-managed lifecycle state:

```bash
devpod status aci-hello
```

## 7. Clean Up

```bash
devpod delete aci-hello
```

Optional Azure cleanup:

```bash
az group delete --name "$AZURE_RESOURCE_GROUP" --yes --no-wait
```

## Troubleshooting

- If `devpod provider add ./dist/provider-local.yaml --name aci-local` fails, confirm `./hack/build.sh` completed and that `dist/provider-local.yaml` exists.
- If Azure auth fails, re-run `az login` and confirm `AZURE_SUBSCRIPTION_ID` matches the intended subscription.
- If Azure returns `MissingSubscriptionRegistration`, run `az provider register --namespace Microsoft.ContainerInstance`, wait for registration to complete, and retry.
- If the image pull fails, confirm `HELLO_WORLD_IMAGE` points to an image that already exists and that the registry auth mode is configured correctly.
- If Azure returns `ImageOsTypeNotMatchContainerGroup`, rebuild and republish the sample image as `linux/amd64`. An Apple Silicon `docker build` without `--platform linux/amd64` will usually push an incompatible `linux/arm64` image.
- If `create` fails immediately, confirm you passed `--provider-option WORKSPACE_IMAGE="$HELLO_WORLD_IMAGE"` and that you are not using a git or local-path workspace source.
- For verbose provider logs, export `DEVPOD_DEBUG=true` before `devpod up`.
