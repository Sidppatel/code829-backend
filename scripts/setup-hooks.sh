#!/usr/bin/env bash

set -euo pipefail

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || { echo "error: $1 is not on PATH; install it and re-run" >&2; exit 1; }
}

require_cmd pre-commit

cd "$(dirname "$0")/.."

pre-commit install
pre-commit install --hook-type commit-msg

if [ ! -f .secrets.baseline ]; then
  echo "Generating initial detect-secrets baseline..."
  detect-secrets scan > .secrets.baseline
fi

echo "Hooks installed. Commits will now run gitleaks + detect-secrets."
