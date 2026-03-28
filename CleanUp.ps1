# CleanUp.ps1 — Remove build artifacts and Docker DB volumes
# Usage: .\CleanUp.ps1 [-All] [-Docker] [-Backend] [-Frontend]
# Docker only manages PostgreSQL 16 + Redis 7 — backend and frontend run natively.

param(
    [switch]$All,
    [switch]$Docker,
    [switch]$Backend,
    [switch]$Frontend
)

$ErrorActionPreference = "Continue"
$root = Split-Path -Parent $PSScriptRoot

# If no flags, default to -All
if (-not ($All -or $Docker -or $Backend -or $Frontend)) {
    $All = $true
}

# -- Docker DB Cleanup (PostgreSQL + Redis volumes only) --
if ($All -or $Docker) {
    Write-Host "`n[Docker] Cleaning database volumes..." -ForegroundColor Cyan

    $dockerDataPath = Join-Path $root "backend\docker-data"
    if (Test-Path $dockerDataPath) {
        Remove-Item -Recurse -Force $dockerDataPath
        Write-Host "  Removed backend\docker-data\ (postgres + redis volumes)" -ForegroundColor Green
    } else {
        Write-Host "  backend\docker-data\ not found — skipping" -ForegroundColor DarkGray
    }
}

# -- Backend Native Build Artifacts --
if ($All -or $Backend) {
    Write-Host "`n[Backend] Cleaning .NET build artifacts..." -ForegroundColor Cyan

    $backendPath = Join-Path $root "backend"
    Get-ChildItem -Path $backendPath -Recurse -Directory -Include "bin","obj" | ForEach-Object {
        Remove-Item -Recurse -Force $_.FullName
        Write-Host "  Removed $($_.FullName)" -ForegroundColor Green
    }

    $uploadsPath = Join-Path $backendPath "uploads"
    if (Test-Path $uploadsPath) {
        Remove-Item -Recurse -Force $uploadsPath
        Write-Host "  Removed backend\uploads\" -ForegroundColor Green
    }

    $envFile = Join-Path $backendPath ".env"
    if (Test-Path $envFile) {
        Write-Host "  Note: backend\.env preserved (not cleaned)" -ForegroundColor Yellow
    }
}

# -- Frontend Native Build Artifacts --
if ($All -or $Frontend) {
    Write-Host "`n[Frontend] Cleaning Node.js build artifacts..." -ForegroundColor Cyan

    $frontendPath = Join-Path $root "frontend"

    $nodeModules = Join-Path $frontendPath "node_modules"
    if (Test-Path $nodeModules) {
        Remove-Item -Recurse -Force $nodeModules
        Write-Host "  Removed frontend\node_modules\" -ForegroundColor Green
    }

    $dist = Join-Path $frontendPath "dist"
    if (Test-Path $dist) {
        Remove-Item -Recurse -Force $dist
        Write-Host "  Removed frontend\dist\" -ForegroundColor Green
    }
}

Write-Host "`n[CleanUp] Done.`n" -ForegroundColor Green
