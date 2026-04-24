#!/usr/bin/env bash
# Dump Postgres schema (tables, views, functions, procedures, triggers, sequences, full schema).
# Mirrors sql_dump.ps1 at the monorepo root.

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib.sh
source "$SCRIPT_DIR/lib.sh"

ROOT="$(resolve_monorepo_root)"

DB="${POSTGRES_DB:-event_platform}"
USER="${POSTGRES_USER:-ep_dev}"
CONTAINER="event-platform-db"

OUT="$ROOT/SQL"
for f in tables views functions procedures triggers sequences schema; do
    mkdir -p "$OUT/$f"
done

log_step "Exporting TABLES..."
mapfile -t tables < <(docker exec "$CONTAINER" psql -U "$USER" -d "$DB" -Atc \
    "SELECT tablename FROM pg_tables WHERE schemaname='public';")
for t in "${tables[@]}"; do
    [ -z "$t" ] && continue
    docker exec "$CONTAINER" pg_dump -U "$USER" -d "$DB" -t "$t" -s > "$OUT/tables/$t.sql"
done

log_step "Exporting VIEWS..."
mapfile -t views < <(docker exec "$CONTAINER" psql -U "$USER" -d "$DB" -Atc \
    "SELECT table_name FROM information_schema.views WHERE table_schema='public';")
for v in "${views[@]}"; do
    [ -z "$v" ] && continue
    docker exec "$CONTAINER" pg_dump -U "$USER" -d "$DB" -t "$v" -s > "$OUT/views/$v.sql"
done

log_step "Exporting FUNCTIONS..."
mapfile -t funcs < <(docker exec "$CONTAINER" psql -U "$USER" -d "$DB" -Atc \
    "SELECT routine_name FROM information_schema.routines WHERE routine_schema='public';")
for fn in "${funcs[@]}"; do
    [ -z "$fn" ] && continue
    docker exec "$CONTAINER" pg_dump -U "$USER" -d "$DB" -Fc -s --function="public.$fn" > "$OUT/functions/$fn.sql"
done

log_step "Exporting PROCEDURES..."
mapfile -t procs < <(docker exec "$CONTAINER" psql -U "$USER" -d "$DB" -Atc \
    "SELECT proname FROM pg_proc p JOIN pg_namespace n ON p.pronamespace=n.oid WHERE n.nspname='public' AND p.prokind='p';")
for p in "${procs[@]}"; do
    [ -z "$p" ] && continue
    docker exec "$CONTAINER" pg_dump -U "$USER" -d "$DB" -s --function="public.$p" > "$OUT/procedures/$p.sql"
done

log_step "Exporting TRIGGERS..."
mapfile -t trigger_tables < <(docker exec "$CONTAINER" psql -U "$USER" -d "$DB" -Atc \
    "SELECT event_object_table FROM information_schema.triggers GROUP BY event_object_table;")
for t in "${trigger_tables[@]}"; do
    [ -z "$t" ] && continue
    docker exec "$CONTAINER" pg_dump -U "$USER" -d "$DB" -t "$t" -s | grep -i TRIGGER > "$OUT/triggers/$t-triggers.sql" || true
done

log_step "Exporting SEQUENCES..."
mapfile -t seqs < <(docker exec "$CONTAINER" psql -U "$USER" -d "$DB" -Atc \
    "SELECT sequence_name FROM information_schema.sequences WHERE sequence_schema='public';")
for s in "${seqs[@]}"; do
    [ -z "$s" ] && continue
    docker exec "$CONTAINER" pg_dump -U "$USER" -d "$DB" -t "$s" -s > "$OUT/sequences/$s.sql"
done

log_step "Exporting FULL SCHEMA..."
docker exec "$CONTAINER" pg_dump -U "$USER" -d "$DB" -s > "$OUT/schema/full_schema.sql"

log_ok "DONE."
