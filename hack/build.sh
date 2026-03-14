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
    
    # Trimming is disabled because the Azure SDK relies on reflection-based JSON serialization.
    dotnet publish \
        "$PROJECT_DIR/src/DevPod.Provider.ACI/DevPod.Provider.ACI.csproj" \
        -c Release \
        -r "$PLATFORM" \
        -p:PublishSingleFile=true \
        -p:SelfContained=true \
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
(
    cd "$BUILD_DIR"
    for FILE in devpod-provider-aci-*; do
        if [ -f "$FILE" ]; then
            sha256sum "$FILE" > "$FILE.sha256"
        fi
    done
)

if ! command -v python3 >/dev/null 2>&1; then
    echo "python3 is required to render the local provider manifest." >&2
    exit 1
fi

checksum_from_file() {
    local file_path="$1"
    if [[ ! -f "$file_path" ]]; then
        echo "Missing checksum file: $file_path" >&2
        exit 1
    fi
    awk '{print $1}' "$file_path"
}

export VERSION
CHECKSUM_LINUX_AMD64="$(checksum_from_file "$BUILD_DIR/devpod-provider-aci-linux-x64.sha256")"
export CHECKSUM_LINUX_AMD64
CHECKSUM_LINUX_ARM64="$(checksum_from_file "$BUILD_DIR/devpod-provider-aci-linux-arm64.sha256")"
export CHECKSUM_LINUX_ARM64
CHECKSUM_DARWIN_AMD64="$(checksum_from_file "$BUILD_DIR/devpod-provider-aci-osx-x64.sha256")"
export CHECKSUM_DARWIN_AMD64
CHECKSUM_DARWIN_ARM64="$(checksum_from_file "$BUILD_DIR/devpod-provider-aci-osx-arm64.sha256")"
export CHECKSUM_DARWIN_ARM64
CHECKSUM_WINDOWS_AMD64="$(checksum_from_file "$BUILD_DIR/devpod-provider-aci-win-x64.exe.sha256")"
export CHECKSUM_WINDOWS_AMD64

export TEMPLATE_PATH="$PROJECT_DIR/provider.yaml"
export OUTPUT_PATH="$BUILD_DIR/provider-local.yaml"
export BINARY_LINUX_AMD64="$BUILD_DIR/devpod-provider-aci-linux-x64"
export BINARY_LINUX_ARM64="$BUILD_DIR/devpod-provider-aci-linux-arm64"
export BINARY_DARWIN_AMD64="$BUILD_DIR/devpod-provider-aci-osx-x64"
export BINARY_DARWIN_ARM64="$BUILD_DIR/devpod-provider-aci-osx-arm64"
export BINARY_WINDOWS_AMD64="$BUILD_DIR/devpod-provider-aci-win-x64.exe"

python3 "$SCRIPT_DIR/render_provider.py"

echo "Local provider manifest written to $OUTPUT_PATH"

echo "Build complete! Artifacts in $BUILD_DIR"
ls -la "$BUILD_DIR"
