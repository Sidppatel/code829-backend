# Stop.ps1 — Stop all development services
# Stops native dotnet + node processes and Docker DB containers.
# Usage: .\Stop.ps1 [-All]
# -All: docker compose down (removes containers, preserves volumes)
# Default: docker compose stop (pauses containers, preserves state)

param(
    [switch]$All
)

$ErrorActionPreference = "Continue"
$root = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$backendPath = Join-Path $root "backend"

# ── Stop Native Backend (dotnet process) ──
Write-Host "`n[Stop] Stopping native backend process..." -ForegroundColor Cyan

$dotnetProcs = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -match "api" -or $_.MainModule.FileName -match "dotnet" }

# Also check for the api.exe process directly
$apiProcs = Get-Process -Name "api" -ErrorAction SilentlyContinue

$stopped = 0
foreach ($proc in @($dotnetProcs; $apiProcs) | Where-Object { $_ -ne $null }) {
    try {
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        $stopped++
    } catch { }
}

if ($stopped -gt 0) {
    Write-Host "  Stopped $stopped backend process(es)" -ForegroundColor Green
} else {
    Write-Host "  No backend processes found" -ForegroundColor DarkGray
}

# ── Stop Native Frontend (node/npm process) ──
Write-Host "[Stop] Stopping native frontend process..." -ForegroundColor Cyan

$nodeProcs = Get-Process -Name "node" -ErrorAction SilentlyContinue

$stopped = 0
foreach ($proc in $nodeProcs) {
    try {
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        $stopped++
    } catch { }
}

if ($stopped -gt 0) {
    Write-Host "  Stopped $stopped frontend process(es)" -ForegroundColor Green
} else {
    Write-Host "  No frontend processes found" -ForegroundColor DarkGray
}

# ── Stop Docker DB Containers (PostgreSQL + Redis) ──
Write-Host "[Stop] Stopping Docker database containers..." -ForegroundColor Cyan

Push-Location $backendPath
if ($All) {
    Write-Host "  Running docker compose down (remove containers, preserve volumes)..." -ForegroundColor Yellow
    & docker compose down
} else {
    Write-Host "  Running docker compose stop (pause containers)..." -ForegroundColor DarkGray
    & docker compose stop
}
Pop-Location

Write-Host "`n[Stop] All services stopped.`n" -ForegroundColor Green
