# Dotnet Hello World Sample

This sample is the source for the published smoke-test image used by the ACI provider.

## What This Sample Is For

- Build and publish `ghcr.io/<your-org>/devpod-provider-aci-hello-world:latest`
- Validate the provider against a direct ACI, image-backed workspace

## What It Is Not For

- The current provider release does not support `devpod up ./samples/dotnet-hello-world`
- The current provider release does not support git or local-path workspace sync

## Build the Image Locally

Run the following from `samples/dotnet-hello-world`:

```bash
docker buildx build --platform linux/amd64 -f Dockerfile -t ghcr.io/<repo-owner>/devpod-provider-aci-hello-world:latest --load ../..
```

The Docker build context must be the repository root so the build can include the shared `.NET` files `Directory.Build.props` and `Directory.Packages.props`. The `-f Dockerfile` flag is required because the Dockerfile lives in this sample directory, not at the repository root.

`--platform linux/amd64` is required for ACI compatibility. On Apple Silicon, omitting it will usually produce a `linux/arm64` image that Azure Container Instances rejects.

Equivalent command from the repository root:

```bash
docker buildx build --platform linux/amd64 -f samples/dotnet-hello-world/Dockerfile -t ghcr.io/<repo-owner>/devpod-provider-aci-hello-world:latest --load .
```

## Publish to GHCR

For a local push, `GITHUB_TOKEN` should be a GitHub Personal Access Token with permission to write packages to `ghcr.io`.

```bash
echo "$GITHUB_TOKEN" | docker login ghcr.io -u <github-user> --password-stdin
docker buildx build --platform linux/amd64 -f samples/dotnet-hello-world/Dockerfile -t ghcr.io/<repo-owner>/devpod-provider-aci-hello-world:latest --push .
```

Create the token in GitHub under `Settings` -> `Developer settings` -> `Personal access tokens`, then export it in your shell before running the commands above.

After pushing the image, use that exact image reference as `HELLO_WORLD_IMAGE` in the e2e guide.

Example:

```bash
export HELLO_WORLD_IMAGE="ghcr.io/<repo-owner>/devpod-provider-aci-hello-world:latest"
```

The repository also includes a GitHub Actions workflow to publish the sample image automatically.

Workflow details:

- file: `.github/workflows/publish-sample-image.yml`
- trigger: push to `main` when the sample or workflow changes, or manual dispatch
- image name: `ghcr.io/<repo-owner>/devpod-provider-aci-hello-world`
- tags pushed: `:latest` and a short `:sha-...` tag
- platform: `linux/amd64` for ACI compatibility

If you rely on the GitHub Actions image, set `HELLO_WORLD_IMAGE` to:

```bash
export HELLO_WORLD_IMAGE="ghcr.io/<repo-owner>/devpod-provider-aci-hello-world:latest"
```
