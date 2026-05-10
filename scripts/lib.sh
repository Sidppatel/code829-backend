#!/usr/bin/env bash

set -euo pipefail

COLOR_CYAN='\033[0;36m'
COLOR_YELLOW='\033[0;33m'
COLOR_GREEN='\033[0;32m'
COLOR_RED='\033[0;31m'
COLOR_GRAY='\033[0;90m'
COLOR_RESET='\033[0m'

log_info()  { printf "${COLOR_CYAN}%s${COLOR_RESET}\n"  "$*"; }
log_step()  { printf "${COLOR_YELLOW}%s${COLOR_RESET}\n" "$*"; }
log_ok()    { printf "${COLOR_GREEN}%s${COLOR_RESET}\n"  "$*"; }
log_err()   { printf "${COLOR_RED}%s${COLOR_RESET}\n"    "$*" >&2; }
log_gray()  { printf "${COLOR_GRAY}%s${COLOR_RESET}\n"   "$*"; }

resolve_monorepo_root() {
    local script_dir
    script_dir="$(cd "$(dirname "${BASH_SOURCE[1]:-$0}")" && pwd)"

    echo "$(cd "$script_dir/../.." && pwd)"
}

require_tool() {
    local tool="$1"
    if ! command -v "$tool" >/dev/null 2>&1; then
        log_err "ERROR: '$tool' is not installed or not on PATH."
        exit 1
    fi
}

parse_env() {
    local line key value
    while IFS= read -r line || [ -n "$line" ]; do

        [[ "$line" =~ ^[[:space:]]*# ]] && continue
        [[ "$line" =~ ^[[:space:]]*$ ]] && continue
        if [[ "$line" =~ ^[[:space:]]*([A-Za-z_][A-Za-z0-9_]*)=(.*)$ ]]; then
            key="${BASH_REMATCH[1]}"
            value="${BASH_REMATCH[2]}"

            if [[ "$value" =~ ^\"(.*)\"$ ]] || [[ "$value" =~ ^\'(.*)\'$ ]]; then
                value="${BASH_REMATCH[1]}"
            fi
            export "$key=$value"
        fi
    done
}

load_infisical_dev() {
    require_tool infisical
    local out
    if ! out=$(infisical export --env=dev --format=dotenv 2>&1); then
        log_err "ERROR: 'infisical export' failed. Run 'infisical login' first."
        log_err "$out"
        exit 1
    fi
    echo "$out" | parse_env
    log_gray "Loaded environment from Infisical (dev)"
}

load_env_local() {
    local root="$1"
    local f="$root/.env.local"
    if [ -f "$f" ]; then
        parse_env < "$f"
        log_gray "Loaded local defaults from .env.local"
    else
        log_step "WARNING: .env.local not found at $f - docker creds + local URLs not set."
    fi
}

write_redis_secret() {
    local backend="$1"
    local secrets_dir="$backend/docker-secrets"
    local secret_file="$secrets_dir/redis_password"
    if [ -z "${REDIS_PASSWORD:-}" ]; then
        log_err "ERROR: REDIS_PASSWORD not set - cannot write $secret_file"
        exit 1
    fi
    mkdir -p "$secrets_dir"
    printf '%s' "$REDIS_PASSWORD" > "$secret_file"
    chmod 600 "$secret_file" 2>/dev/null || true
    log_gray "Wrote Redis password to docker-secrets/redis_password (gitignored)"
}

wait_for_port() {
    local host="$1" port="$2" timeout="${3:-30}" i
    for ((i=1; i<=timeout; i++)); do
        if (exec 3<>"/dev/tcp/$host/$port") 2>/dev/null; then
            exec 3<&- 3>&-
            return 0
        fi
        sleep 1
    done
    return 1
}

check_docker_ready() {
    local max_wait="${1:-30}" i db_ready redis_ready
    for ((i=1; i<=max_wait; i++)); do
        db_ready=$(docker exec event-platform-db pg_isready -U "${POSTGRES_USER:-ep_dev}" 2>/dev/null || true)
        redis_ready=$(docker exec event-platform-redis sh -c 'redis-cli -a "$(cat /run/secrets/redis_password)" ping 2>/dev/null' 2>/dev/null || true)
        if [[ "$db_ready" == *"accepting connections"* ]] && [[ "$redis_ready" == *"PONG"* ]]; then
            log_ok "Database and Redis are ready!"
            return 0
        fi
        sleep 1
    done
    return 1
}

track_pid() {
    local file="$1"; shift
    local json="{"
    local first=1
    while [ $# -ge 2 ]; do
        if [ $first -eq 0 ]; then json+=","; fi
        json+="\"$1\":$2"
        first=0
        shift 2
    done
    json+="}"
    echo "$json" > "$file"
}
