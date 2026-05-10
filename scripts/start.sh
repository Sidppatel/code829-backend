#!/usr/bin/env bash

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

source "$SCRIPT_DIR/lib.sh"

ROOT="$(resolve_monorepo_root)"
BACKEND="$ROOT/code829-backend"
FRONTEND="$ROOT/code829-frontend"

log_info "Starting Event Platform..."

cd "$BACKEND"
load_infisical_dev
cd "$ROOT"
load_env_local "$ROOT"

for tool in docker dotnet pnpm; do require_tool "$tool"; done

log_step "Starting Docker containers..."
docker rm -f event-platform-db event-platform-redis >/dev/null 2>&1 || true
write_redis_secret "$BACKEND"
docker compose -f "$BACKEND/docker-compose.yml" up -d

log_step "Waiting for database and Redis..."
if ! check_docker_ready 30; then
    log_err "WARNING: Timed out waiting for containers. Starting backend anyway..."
fi

if [ ! -d "$FRONTEND/node_modules" ]; then
    log_step "Installing frontend dependencies (pnpm)..."
    (cd "$FRONTEND" && pnpm install)
fi

log_step "Starting backend..."
(cd "$BACKEND" && dotnet run --project api/api.csproj > "$ROOT/.ep-backend.log" 2>&1) &
BACKEND_PID=$!
disown $BACKEND_PID || true

log_step "Starting frontend apps..."
(cd "$FRONTEND" && pnpm dev:public    > "$ROOT/.ep-public.log"    2>&1) & PUBLIC_PID=$!;    disown $PUBLIC_PID    || true
(cd "$FRONTEND" && pnpm dev:admin     > "$ROOT/.ep-admin.log"     2>&1) & ADMIN_PID=$!;     disown $ADMIN_PID     || true
(cd "$FRONTEND" && pnpm dev:staff     > "$ROOT/.ep-staff.log"     2>&1) & STAFF_PID=$!;     disown $STAFF_PID     || true
(cd "$FRONTEND" && pnpm dev:developer > "$ROOT/.ep-developer.log" 2>&1) & DEVELOPER_PID=$!; disown $DEVELOPER_PID || true

echo ""
log_ok "Event Platform started!"
log_info "  Backend        http://localhost:8000"
log_info "  Public         http://localhost:5173"
log_info "  Admin          http://localhost:5174"
log_info "  Staff          http://localhost:5175"
log_info "  Developer      http://localhost:5176"

track_pid "$ROOT/.ep-pids.json" \
    BackendPid   "$BACKEND_PID" \
    PublicPid    "$PUBLIC_PID" \
    AdminPid     "$ADMIN_PID" \
    StaffPid     "$STAFF_PID" \
    DeveloperPid "$DEVELOPER_PID"
