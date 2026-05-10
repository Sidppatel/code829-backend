#!/usr/bin/env bash

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

source "$SCRIPT_DIR/lib.sh"

ROOT="$(resolve_monorepo_root)"
BACKEND="$ROOT/code829-backend"

log_info "Restarting Event Platform..."

"$SCRIPT_DIR/stop.sh"

log_step "Clearing backend build artifacts..."
sleep 2
for proj in api db contracts; do
    rm -rf "$BACKEND/$proj/bin" "$BACKEND/$proj/obj" 2>/dev/null || true
done

"$SCRIPT_DIR/start.sh"
