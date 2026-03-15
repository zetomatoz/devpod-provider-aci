# Dotnet Hello World Sample

This sample is the source for the published smoke-test image used by the ACI provider.

## What This Sample Is For

- Build and publish `ghcr.io/zetomatoz/devpod-provider-aci-hello-world:latest`
- Validate the provider against a direct ACI, image-backed workspace

## What It Is Not For

- The current provider release does not support `devpod up ./samples/dotnet-hello-world`
- The current provider release does not support git or local-path workspace sync

## Build the Image Locally

```bash
docker build -t ghcr.io/<your-org>/devpod-provider-aci-hello-world:dev .
```

## Publish to GHCR

```bash
echo "$GITHUB_TOKEN" | docker login ghcr.io -u <github-user> --password-stdin
docker push ghcr.io/<your-org>/devpod-provider-aci-hello-world:dev
```

After pushing the image, use that exact image reference as `HELLO_WORLD_IMAGE` in the e2e guide.

Example:

```bash
export HELLO_WORLD_IMAGE="ghcr.io/<your-org>/devpod-provider-aci-hello-world:dev"
```

The repository also includes a GitHub Actions workflow to publish the sample image automatically.
