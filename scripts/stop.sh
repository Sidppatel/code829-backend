#!/usr/bin/env bash
# Stop all dev processes tracked in .ep-pids.json / .ep-backend-pid.json and docker containers.

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib.sh
source "$SCRIPT_DIR/lib.sh"

ROOT="$(resolve_monorepo_root)"
BACKEND="$ROOT/code829-backend"

log_info "Stopping Event Platform..."

kill_tree() {
    local pid="$1"
    [ -z "$pid" ] && return 0
    if kill -0 "$pid" 2>/dev/null; then
        # kill the process group (negative pid) to take down children
        kill -TERM -"$pid" 2>/dev/null || kill -TERM "$pid" 2>/dev/null || true
        sleep 1
        kill -KILL -"$pid" 2>/dev/null || kill -KILL "$pid" 2>/dev/null || true
    fi
}

for pidfile in "$ROOT/.ep-pids.json" "$ROOT/.ep-backend-pid.json"; do
    if [ -f "$pidfile" ]; then
        # Extract numeric values with grep (avoid jq dependency)
        while IFS=: read -r key pid; do
            pid="${pid//[^0-9]/}"
            [ -z "$pid" ] && continue
            if kill -0 "$pid" 2>/dev/null; then
                log_step "Stopped $(echo "$key" | tr -d '\" ') (PID: $pid)"
                kill_tree "$pid"
            fi
        done < <(grep -oE '"[A-Za-z]+Pid"[[:space:]]*:[[:space:]]*[0-9]+' "$pidfile")
        rm -f "$pidfile"
    fi
done

# Fallback: kill lingering dotnet / node dev-server processes launched from the workspace.
# Conservative: only match explicit project paths to avoid killing unrelated work.
pkill -f "dotnet.*api/api.csproj" 2>/dev/null || true
pkill -f "pnpm dev:(public|admin|staff|developer)" 2>/dev/null || true

log_step "Stopping Docker containers..."
docker compose -f "$BACKEND/docker-compose.yml" stop

log_ok "Event Platform stopped!"
