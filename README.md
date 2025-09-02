# DevPod Provider for Azure Container Instances

Run your dev containers **serverlessly** on Azure Container Instances (ACI) with DevPod!

## 🚀 Features

- **Serverless Development**: No VMs to manage - containers run directly on ACI
- **Cost Effective**: Pay only for the compute resources you use (per-second billing)
- **Fast Startup**: Containers start in seconds, not minutes
- **Flexible Resources**: Configure CPU, memory, and GPU as needed
- **Persistent Storage**: Optional Azure File Share integration for workspace persistence
- **Network Integration**: Support for both public and private (VNet) deployments
- **Container Registry**: Seamless integration with Azure Container Registry
- **Auto-shutdown**: Configurable inactivity timeout to save costs

## 📋 Prerequisites

- DevPod CLI installed ([Installation Guide](https://devpod.sh/docs/getting-started/install))
- Azure subscription with ACI service enabled
- Azure CLI installed and configured (or service principal credentials)
- .NET 8 SDK (for building from source)

## 🔧 Installation

### Option 1: Install from Release

```bash
# Add the provider from GitHub releases
devpod provider add github.com/your-org/devpod-provider-aci

# Or add from a specific release
devpod provider add https://github.com/your-org/devpod-provider-aci/releases/download/v0.1.0/provider.yaml
```

### Option 2: Build from Source

```bash
# Clone the repository
git clone https://github.com/zetomatoz/devpod-provider-aci
cd devpod-provider-aci

# Build the provider
./hack/build.sh  # On Linux/macOS
# OR
./hack/build.ps1 # On Windows

# Add the local provider
devpod provider add ./provider.yaml --name aci-local
```

## 🛠️ Development

For detailed development setup, building instructions, and project structure, see [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).