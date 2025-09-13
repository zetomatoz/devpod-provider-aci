# Development Guide

This document provides development setup instructions and project structure details for the DevPod Provider ACI.

## Repo Structure

```
devpod-provider-aci/
├── src/
│   ├── DevPod.Provider.ACI/
│   │   ├── DevPod.Provider.ACI.csproj
│   │   ├── Program.cs
│   │   ├── Commands/
│   │   │   ├── InitCommand.cs
│   │   │   ├── CreateCommand.cs
│   │   │   ├── DeleteCommand.cs
│   │   │   ├── StartCommand.cs
│   │   │   ├── StopCommand.cs
│   │   │   ├── StatusCommand.cs
│   │   │   └── CommandCommand.cs
│   │   ├── Services/
│   │   │   ├── IAciService.cs
│   │   │   ├── AciService.cs
│   │   │   ├── IAuthenticationService.cs
│   │   │   ├── AuthenticationService.cs
│   │   │   ├── IProviderOptionsService.cs
│   │   │   └── ProviderOptionsService.cs
│   │   ├── Models/
│   │   │   ├── ProviderOptions.cs
│   │   │   ├── ContainerGroupDefinition.cs
│   │   │   └── ContainerStatus.cs
│   │   ├── Infrastructure/
│   │   │   ├── Constants.cs
│   │   │   ├── Logger.cs
│   │   │   └── CommandRouter.cs
│   │   └── appsettings.json
│   │
│   └── DevPod.Provider.ACI.Tests/
│       ├── DevPod.Provider.ACI.Tests.csproj
│       ├── Unit/
│       │   ├── Services/
│       │   │   ├── AciServiceTests.cs
│       │   │   └── ProviderOptionsServiceTests.cs
│       │   └── Commands/
│       │       └── CommandTests.cs
│       └── Integration/
│           └── AciProviderIntegrationTests.cs
│
├── provider.yaml
├── hack/
│   ├── build.sh
│   ├── build.ps1
│   └── release.sh
├── samples/
│   ├── dotnet-hello-world/
│   │   ├── .devcontainer/
│   │   │   └── devcontainer.json
│   │   ├── HelloWorld.csproj
│   │   ├── Program.cs
│   │   └── README.md
│   └── aspire-sample/
│       ├── .devcontainer/
│       │   └── devcontainer.json
│       └── AspireSample.AppHost/
├── docs/
│   ├── README.md
│   ├── ARCHITECTURE.md
│   └── DEVELOPMENT.md
├── .github/
│   └── workflows/
│       ├── build.yml
│       ├── test.yml
│       └── release.yml
├── DevPod.Provider.ACI.sln
├── global.json
├── Directory.Build.props
├── .editorconfig
├── .gitignore
└── README.md
```

## Prerequisites

- .NET 8 SDK
- Azure CLI (optional, for CLI-based authentication)
- DevPod CLI for testing

## Building from Source

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

## Testing

### Unit Tests
```bash
dotnet test src/DevPod.Provider.ACI.Tests/DevPod.Provider.ACI.Tests.csproj
```

### Integration Tests
Integration tests require Azure credentials and may create actual resources:

```bash
# Set up Azure credentials
az login
export AZURE_SUBSCRIPTION_ID="your-subscription-id"
export AZURE_RESOURCE_GROUP="test-rg"
export AZURE_REGION="eastus"

# Run integration tests
dotnet test src/DevPod.Provider.ACI.Tests/DevPod.Provider.ACI.Tests.csproj --filter Category=Integration
```

## Local Development and Testing

### Install Local Provider
```bash
# Build the provider first
./hack/build.sh

# Add the local provider to DevPod
devpod provider add ./provider.yaml --name aci-local
```

### Test with DevPod
```bash
# Set required Azure environment variables
export AZURE_SUBSCRIPTION_ID="your-subscription-id"
export AZURE_RESOURCE_GROUP="devpod-test-rg"
export AZURE_REGION="eastus"

# Create a workspace
devpod up ./samples/dotnet-hello-world --provider aci-local

# Test provider commands directly
./dist/devpod-provider-aci init
./dist/devpod-provider-aci create
./dist/devpod-provider-aci status
```

## Debug Mode

Enable debug logging by setting the environment variable:

```bash
export DEVPOD_DEBUG=true
```

## Architecture

For detailed architecture information, see [ARCHITECTURE.md](./ARCHITECTURE.md).

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Run tests and ensure they pass
6. Submit a pull request

## Release Process

Releases are automated via GitHub Actions. To create a new release:

1. Update version in `provider.yaml`
2. Create a git tag: `git tag v0.x.x`
3. Push the tag: `git push origin v0.x.x`
4. GitHub Actions will build and create the release automatically