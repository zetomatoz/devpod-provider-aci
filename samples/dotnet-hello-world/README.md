# Dotnet Hello World Sample

This sample is the source for the published smoke-test image used by the ACI provider.

## What This Sample Is For

- Build and publish `ghcr.io/<your-org>/devpod-provider-aci-hello-world:latest`
- Validate the provider against a direct ACI, image-backed workspace

## What It Is Not For

- The current provider release does not support `devpod up ./samples/dotnet-hello-world`
- The current provider release does not support git or local-path workspace sync

## Build the Image Locally

```bash
docker build -t ghcr.io/<your-org>/devpod-provider-aci-hello-world:latest .
```

## Publish to GHCR

```bash
echo "$GITHUB_TOKEN" | docker login ghcr.io -u <github-user> --password-stdin
docker push ghcr.io/<your-org>/devpod-provider-aci-hello-world:latest
```

After pushing the image, use that exact image reference as `HELLO_WORLD_IMAGE` in the e2e guide.

Example:

```bash
export HELLO_WORLD_IMAGE="ghcr.io/<your-org>/devpod-provider-aci-hello-world:latest"
```

The repository also includes a GitHub Actions workflow to publish the sample image automatically.

Workflow details:

- file: `.github/workflows/publish-sample-image.yml`
- trigger: push to `main` when the sample or workflow changes, or manual dispatch
- image name: `ghcr.io/<repo-owner>/devpod-provider-aci-hello-world`
- tags pushed: `:latest` and a short `:sha-...` tag

If you rely on the GitHub Actions image, set `HELLO_WORLD_IMAGE` to:

```bash
export HELLO_WORLD_IMAGE="ghcr.io/<repo-owner>/devpod-provider-aci-hello-world:latest"
```
