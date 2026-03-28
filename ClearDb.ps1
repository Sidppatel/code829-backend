# ClearDb.ps1 — Destroy PostgreSQL and Redis completely
# Stops all dependent native processes, removes DB containers, images, and volumes.
# This script ONLY touches database resources — no backend/frontend files.
# Usage: .\ClearDb.ps1

$ErrorActionPreference = "Continue"
$root = Split-Path -Parent $PSScriptRoot
$backendPath = Join-Path $root "backend"

Write-Host "`n[ClearDb] Destroying PostgreSQL and Redis..." -ForegroundColor Red

# ── Step 1: Stop native processes that depend on the DB ──
Write-Host "`n[1/4] Stopping native processes that depend on the database..." -ForegroundColor Cyan
& "$PSScriptRoot\Stop.ps1"

# ── Step 2: docker compose down -v (containers + volumes) ──
Write-Host "[2/4] Running docker compose down -v (remove containers + named volumes)..." -ForegroundColor Cyan

Push-Location $backendPath
& docker compose down -v
Pop-Location

# ── Step 3: Remove local docker-data directory ──
Write-Host "[3/4] Removing local docker-data/ directory..." -ForegroundColor Cyan

$dockerDataPath = Join-Path $backendPath "docker-data"
if (Test-Path $dockerDataPath) {
    Remove-Item -Recurse -Force $dockerDataPath
    Write-Host "  Removed backend\docker-data\" -ForegroundColor Green
} else {
    Write-Host "  backend\docker-data\ not found — already clean" -ForegroundColor DarkGray
}

# ── Step 4: Remove Docker images ──
Write-Host "[4/4] Removing database Docker images..." -ForegroundColor Cyan

$images = @("postgres:16-alpine", "redis:7-alpine")
foreach ($img in $images) {
    $exists = docker images -q $img 2>$null
    if ($exists) {
        & docker rmi $img 2>$null
        Write-Host "  Removed image: $img" -ForegroundColor Green
    } else {
        Write-Host "  Image not found: $img — skipping" -ForegroundColor DarkGray
    }
}

Write-Host "`n[ClearDb] Database fully destroyed. Run .\Startup.ps1 to recreate.`n" -ForegroundColor Green
