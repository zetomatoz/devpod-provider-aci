# End-to-End Smoke Test: Hello World Image on ACI

This guide validates the current provider contract: a published image workspace running directly on Azure Container Instances.

Important constraints for this release:

- Use a published image source such as `ghcr.io/zetomatoz/devpod-provider-aci-hello-world:latest`
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
```

Export the required provider settings:

```bash
export AZURE_SUBSCRIPTION_ID="<SUBSCRIPTION_ID>"
export AZURE_RESOURCE_GROUP="devpod-aci-e2e"
export AZURE_REGION="westus2"
export HELLO_WORLD_IMAGE="ghcr.io/zetomatoz/devpod-provider-aci-hello-world:latest"
```

The provider will create the resource group automatically if it does not already exist.

## 2. Build and Register the Provider

Use the local packaging script, not plain `dotnet publish`.

```bash
./hack/build.sh
devpod provider add ./dist/provider-local.yaml --name aci-local
```

Why this step matters:

- `dotnet build` compiles the provider code
- `./hack/build.sh` packages the provider for DevPod
- `./hack/build.sh` calls `hack/render_provider.py`
- `hack/render_provider.py` transforms [provider.yaml](/Users/Thomas_1/Sites/devpod-provider-aci/provider.yaml) into `dist/provider-local.yaml` by substituting binary paths and checksums

If `dist/provider-local.yaml` does not exist, DevPod has nothing concrete to install locally.

Optional compile-only checks before packaging:

```bash
dotnet test src/DevPod.Provider.ACI.Tests/DevPod.Provider.ACI.Tests.csproj
dotnet build DevPod.Provider.ACI.sln
```

## 3. Launch the Workspace

Use the published image as the DevPod source:

```bash
devpod up "$HELLO_WORLD_IMAGE" \
  --provider aci-local \
  --workspace aci-hello \
  --ide none
```

Expected behavior:

- DevPod invokes `create`
- The provider provisions an ACI container group from `WORKSPACE_IMAGE`
- DevPod injects the agent using the provider `command` hook and the ACI exec WebSocket
- The workspace reaches a ready state

## 4. Validate the Running App

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

## 5. Clean Up

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
- If the image pull fails, confirm the image exists and the registry auth mode is configured correctly.
- If `create` fails immediately, check whether `WORKSPACE_IMAGE` is set and that you are not using a git or local-path workspace source.
- For verbose provider logs, export `DEVPOD_DEBUG=true` before `devpod up`.
