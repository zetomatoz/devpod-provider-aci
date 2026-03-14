#!/usr/bin/env python3

import os
import sys
from pathlib import Path
from string import Template


REQUIRED_KEYS = [
    "VERSION",
    "CHECKSUM_LINUX_AMD64",
    "CHECKSUM_LINUX_ARM64",
    "CHECKSUM_DARWIN_AMD64",
    "CHECKSUM_DARWIN_ARM64",
    "CHECKSUM_WINDOWS_AMD64",
    "BINARY_LINUX_AMD64",
    "BINARY_LINUX_ARM64",
    "BINARY_DARWIN_AMD64",
    "BINARY_DARWIN_ARM64",
    "BINARY_WINDOWS_AMD64",
    "TEMPLATE_PATH",
    "OUTPUT_PATH",
]


def main() -> int:
    missing = [key for key in REQUIRED_KEYS if not os.environ.get(key)]
    if missing:
        print(f"Missing variables for templating: {', '.join(sorted(missing))}", file=sys.stderr)
        return 1

    template_path = Path(os.environ["TEMPLATE_PATH"])
    output_path = Path(os.environ["OUTPUT_PATH"])

    try:
        template_content = template_path.read_text()
    except FileNotFoundError as exc:
        print(f"Template not found: {exc}", file=sys.stderr)
        return 1

    values = {key: os.environ[key] for key in REQUIRED_KEYS if key not in {"TEMPLATE_PATH", "OUTPUT_PATH"}}
    rendered = Template(template_content).safe_substitute(values)

    if not rendered.endswith("\n"):
        rendered += "\n"

    output_path.write_text(rendered)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
