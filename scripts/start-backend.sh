#!/usr/bin/env bash
# Boot backend only: docker + API. Skips frontend apps.

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib.sh
source "$SCRIPT_DIR/lib.sh"

ROOT="$(resolve_monorepo_root)"
BACKEND="$ROOT/code829-backend"

log_info "Starting Backend (Docker + API)..."

cd "$BACKEND"
load_infisical_dev
cd "$ROOT"
load_env_local "$ROOT"

for tool in docker dotnet; do require_tool "$tool"; done

log_step "Starting Docker containers..."
docker rm -f event-platform-db event-platform-redis >/dev/null 2>&1 || true
write_redis_secret "$BACKEND"
docker compose -f "$BACKEND/docker-compose.yml" up -d

log_step "Waiting for database and Redis..."
if ! check_docker_ready 30; then
    log_err "WARNING: Timed out waiting for containers. Starting backend anyway..."
fi

log_step "Starting backend API..."
(cd "$BACKEND" && dotnet run --project api/api.csproj > "$ROOT/.ep-backend.log" 2>&1) &
BACKEND_PID=$!
disown $BACKEND_PID || true

echo ""
log_ok "Backend started!"
log_info "  API  http://localhost:8000"
log_gray "  PID  $BACKEND_PID"
log_gray "  Logs $ROOT/.ep-backend.log"

track_pid "$ROOT/.ep-backend-pid.json" BackendPid "$BACKEND_PID"
