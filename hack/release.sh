#!/usr/bin/env bash

set -euo pipefail

usage() {
  echo "Usage: $(basename "$0") [--publish] <version>" >&2
  echo "  --publish    Upload artifacts to the GitHub release for the provided version" >&2
  echo "Example: $(basename "$0") --publish 0.2.0" >&2
}

PUBLISH=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --publish)
      PUBLISH=true
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      break
      ;;
  esac
done

if [[ $# -ne 1 ]]; then
  usage
  exit 1
fi

VERSION="$1"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
DIST_DIR="$PROJECT_DIR/dist"
TEMPLATE_PATH="$PROJECT_DIR/provider.yaml"
OUTPUT_PATH="$DIST_DIR/provider.yaml"

if ! command -v python3 >/dev/null 2>&1; then
  echo "python3 is required to render the release manifest." >&2
  exit 1
fi

if "$PUBLISH" && ! command -v gh >/dev/null 2>&1; then
  echo "GitHub CLI (gh) is required when using --publish." >&2
  exit 1
fi

echo "==> Building release artifacts for v${VERSION}"
RELEASE_VERSION="$VERSION" "$SCRIPT_DIR/build.sh"

checksum_from_file() {
  local file_path="$1"
  if [[ ! -f "$file_path" ]]; then
    echo "Missing checksum file: $file_path" >&2
    exit 1
  fi
  awk '{print $1}' "$file_path"
}

export VERSION
CHECKSUM_LINUX_AMD64="$(checksum_from_file "$DIST_DIR/devpod-provider-aci-linux-x64.sha256")"
export CHECKSUM_LINUX_AMD64
CHECKSUM_LINUX_ARM64="$(checksum_from_file "$DIST_DIR/devpod-provider-aci-linux-arm64.sha256")"
export CHECKSUM_LINUX_ARM64
CHECKSUM_DARWIN_AMD64="$(checksum_from_file "$DIST_DIR/devpod-provider-aci-osx-x64.sha256")"
export CHECKSUM_DARWIN_AMD64
CHECKSUM_DARWIN_ARM64="$(checksum_from_file "$DIST_DIR/devpod-provider-aci-osx-arm64.sha256")"
export CHECKSUM_DARWIN_ARM64
CHECKSUM_WINDOWS_AMD64="$(checksum_from_file "$DIST_DIR/devpod-provider-aci-win-x64.exe.sha256")"
export CHECKSUM_WINDOWS_AMD64

export TEMPLATE_PATH OUTPUT_PATH

for var in VERSION CHECKSUM_LINUX_AMD64 CHECKSUM_LINUX_ARM64 CHECKSUM_DARWIN_AMD64 CHECKSUM_DARWIN_ARM64 CHECKSUM_WINDOWS_AMD64; do
  if [[ "${!var}" =~ [^a-zA-Z0-9._-] ]]; then
    echo "Invalid characters in $var" >&2
    exit 1
  fi
done

python3 <<'PY'
import os
from pathlib import Path
from string import Template

template_path = Path(os.environ["TEMPLATE_PATH"])
output_path = Path(os.environ["OUTPUT_PATH"])

keys = [
    "VERSION",
    "CHECKSUM_LINUX_AMD64",
    "CHECKSUM_LINUX_ARM64",
    "CHECKSUM_DARWIN_AMD64",
    "CHECKSUM_DARWIN_ARM64",
    "CHECKSUM_WINDOWS_AMD64",
]

missing = [key for key in keys if not os.environ.get(key)]
if missing:
    raise SystemExit(f"Missing variables for templating: {', '.join(missing)}")

values = {key: os.environ[key] for key in keys}
template_content = template_path.read_text()
rendered = Template(template_content).safe_substitute(values)

lines = rendered.splitlines()
for idx, line in enumerate(lines):
    stripped = line.strip()
    if stripped.startswith("version:"):
        prefix = line[: line.find("version:")]
        lines[idx] = f"{prefix}version: v{values['VERSION']}"
        break

rendered = "\n".join(lines)
if template_content.endswith("\n"):
    rendered += "\n"

output_path.write_text(rendered)
PY

echo "==> Release manifest written to $OUTPUT_PATH"
echo "==> Upload binaries from $DIST_DIR along with provider.yaml to the GitHub release"

if "$PUBLISH"; then
  TAG="v${VERSION}"
  echo "==> Preparing to publish assets to GitHub release ${TAG}"

  assets=(
    "$DIST_DIR/devpod-provider-aci-linux-x64"
    "$DIST_DIR/devpod-provider-aci-linux-x64.sha256"
    "$DIST_DIR/devpod-provider-aci-linux-arm64"
    "$DIST_DIR/devpod-provider-aci-linux-arm64.sha256"
    "$DIST_DIR/devpod-provider-aci-osx-x64"
    "$DIST_DIR/devpod-provider-aci-osx-x64.sha256"
    "$DIST_DIR/devpod-provider-aci-osx-arm64"
    "$DIST_DIR/devpod-provider-aci-osx-arm64.sha256"
    "$DIST_DIR/devpod-provider-aci-win-x64.exe"
    "$DIST_DIR/devpod-provider-aci-win-x64.exe.sha256"
    "$OUTPUT_PATH"
  )

  for asset in "${assets[@]}"; do
    if [[ ! -f "$asset" ]]; then
      echo "Missing asset: $asset" >&2
      exit 1
    fi
  done

  if gh release view "$TAG" >/dev/null 2>&1; then
    echo "==> Uploading assets to existing release ${TAG}"
    gh release upload "$TAG" "${assets[@]}" --clobber
  else
    echo "==> Creating GitHub release ${TAG}"
    gh release create "$TAG" "${assets[@]}" \
      --title "v${VERSION}" \
      --notes "Automated release for v${VERSION}"
  fi
fi
