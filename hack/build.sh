#!/bin/bash

set -e

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_DIR="$( cd "$SCRIPT_DIR/.." && pwd )"
VERSION="${RELEASE_VERSION:-0.1.0}"
BUILD_DIR="$PROJECT_DIR/dist"

echo "Building DevPod ACI Provider v$VERSION"

# Clean build directory
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"

# Build for different platforms
PLATFORMS=(
    "linux-x64"
    "linux-arm64"
    "osx-x64"
    "osx-arm64"
    "win-x64"
)

for PLATFORM in "${PLATFORMS[@]}"; do
    echo "Building for $PLATFORM..."
    
    OUTPUT_DIR="$BUILD_DIR/$PLATFORM"
    mkdir -p "$OUTPUT_DIR"
    
    dotnet publish \
        "$PROJECT_DIR/src/DevPod.Provider.ACI/DevPod.Provider.ACI.csproj" \
        -c Release \
        -r "$PLATFORM" \
        -p:PublishSingleFile=true \
        -p:SelfContained=true \
        -p:PublishTrimmed=true \
        -p:PublishReadyToRun=true \
        -p:Version="$VERSION" \
        -o "$OUTPUT_DIR"
    
    # Rename output file
    if [[ "$PLATFORM" == win-* ]]; then
        mv "$OUTPUT_DIR/devpod-provider-aci.exe" "$BUILD_DIR/devpod-provider-aci-${PLATFORM}.exe"
    else
        mv "$OUTPUT_DIR/devpod-provider-aci" "$BUILD_DIR/devpod-provider-aci-${PLATFORM}"
        chmod +x "$BUILD_DIR/devpod-provider-aci-${PLATFORM}"
    fi
    
    # Clean up
    rm -rf "$OUTPUT_DIR"
done

# Generate checksums
echo "Generating checksums..."
cd "$BUILD_DIR"
for FILE in devpod-provider-aci-*; do
    if [ -f "$FILE" ]; then
        sha256sum "$FILE" > "$FILE.sha256"
    fi
done

echo "Build complete! Artifacts in $BUILD_DIR"
ls -la "$BUILD_DIR"