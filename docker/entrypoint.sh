#!/bin/sh
set -e

echo "[entrypoint] applying migrations via code829-db MigrationRunner"
dotnet /app/migrate/MigrationRunner.dll
echo "[entrypoint] migrations done; starting api"

exec dotnet /app/api.dll
