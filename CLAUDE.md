# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a skeleton repository for "devpod-provider-aci" - a DevPod provider for Azure Container Instance. The repository currently contains only basic setup files:

- `README.md`: Basic project description 
- `LICENSE`: MIT license
- `.gitignore`: Python-focused gitignore with comprehensive exclusions

## Repository State

This is a freshly initialized repository on the `bootstrap` branch with minimal content. The actual DevPod provider implementation has not yet been created.

## Development Setup

No build system, package management, or development commands are currently configured. The project will likely need:

- C# language setup (typical for DevPod providers)
- Build configuration for DevPod provider plugin
- Azure SDK dependencies for ACI integration
- Testing framework setup

## Architecture Notes

DevPod providers typically:
- Implement the DevPod provider interface
- Handle container lifecycle management
- Manage cloud resource provisioning/deprovisioning
- Provide configuration for development environment setup

For ACI specifically, this would involve:
- Azure authentication and resource management
- Container Instance creation/deletion
- Networking and storage configuration
- Environment variable and secret management