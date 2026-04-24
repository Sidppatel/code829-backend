#!/usr/bin/env bash
# Full reset: stop, drop Docker volumes + data, clear builds, restore, migrate, start.

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib.sh
source "$SCRIPT_DIR/lib.sh"

ROOT="$(resolve_monorepo_root)"
BACKEND="$ROOT/code829-backend"
FRONTEND="$ROOT/code829-frontend"

log_info "Full reset of Event Platform..."

"$SCRIPT_DIR/stop.sh"

log_step "Clearing backend builds..."
for proj in api db contracts; do
    rm -rf "$BACKEND/$proj/bin" "$BACKEND/$proj/obj" 2>/dev/null || true
done

log_step "Clearing frontend builds..."
for app in public admin staff developer; do
    rm -rf "$FRONTEND/apps/$app/dist" 2>/dev/null || true
done

log_step "Dropping Docker containers and clearing DB data..."
docker compose -f "$BACKEND/docker-compose.yml" down -v
docker rm -f event-platform-db event-platform-redis >/dev/null 2>&1 || true
sleep 3
if [ -d "$BACKEND/docker-data" ]; then
    rm -rf "$BACKEND/docker-data" 2>/dev/null || true
    if [ -d "$BACKEND/docker-data" ]; then
        log_step "WARNING: docker-data still present - files may be locked. Retrying..."
        sleep 3
        rm -rf "$BACKEND/docker-data" 2>/dev/null || true
    fi
    if [ -d "$BACKEND/docker-data" ]; then
        log_err "WARNING: docker-data still present - continuing anyway (DB will use existing data)."
    else
        log_gray "Cleared docker-data"
    fi
fi

cd "$BACKEND"
load_infisical_dev
cd "$ROOT"
load_env_local "$ROOT"

log_step "Starting Docker containers..."
docker compose -f "$BACKEND/docker-compose.yml" up -d

log_step "Waiting for database and Redis..."
if ! check_docker_ready 60; then
    log_err "ERROR: Timed out waiting for database/Redis."
    exit 1
fi

if ! dotnet tool list -g 2>/dev/null | grep -q dotnet-ef; then
    log_step "Installing dotnet-ef global tool..."
    dotnet tool install --global dotnet-ef
fi

log_step "Restoring NuGet packages..."
dotnet restore "$BACKEND/backend.slnx"

log_step "Running EF Core migrations..."
export ASPNETCORE_ENVIRONMENT=Development
dotnet ef database update --project "$BACKEND/db/db.csproj" --startup-project "$BACKEND/api/api.csproj"

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
log_ok "Full reset complete!"
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
