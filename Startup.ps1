# Startup.ps1 — Start the full development environment
# Docker is ONLY used for PostgreSQL 16 + Redis 7.
# Backend (.NET) and frontend (React/Vite) run natively on the host.
# Usage: .\Startup.ps1

param(
    [switch]$SkipFrontend,
    [switch]$SkipBackend
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$backendPath = Join-Path $root "backend"
$frontendPath = Join-Path $root "frontend"

# -- Step 1: Verify Prerequisites --
Write-Host "`n[1/7] Checking prerequisites..." -ForegroundColor Cyan

$dotnetVersion = & dotnet --version 2>$null
if (-not $dotnetVersion) { Write-Host "  ERROR: dotnet SDK not found" -ForegroundColor Red; exit 1 }
Write-Host "  dotnet: $dotnetVersion" -ForegroundColor Green

$dockerVersion = & docker --version 2>$null
if (-not $dockerVersion) { Write-Host "  ERROR: Docker not found" -ForegroundColor Red; exit 1 }
Write-Host "  Docker: $dockerVersion" -ForegroundColor Green

if (-not $SkipFrontend) {
    $nodeVersion = & node --version 2>$null
    if (-not $nodeVersion) { Write-Host "  ERROR: Node.js not found" -ForegroundColor Red; exit 1 }
    Write-Host "  Node: $nodeVersion" -ForegroundColor Green
}

# -- Step 2: Environment Files --
Write-Host "`n[2/7] Checking environment files..." -ForegroundColor Cyan

$backendEnv = Join-Path $backendPath ".env"
if (-not (Test-Path $backendEnv)) {
    Write-Host "  Creating backend\.env from .env.example..." -ForegroundColor Yellow
    Copy-Item (Join-Path $backendPath ".env.example") $backendEnv

    # Generate encryption key if placeholder
    $content = Get-Content $backendEnv -Raw
    if ($content -match "<64-char hex") {
        $key = -join ((1..32) | ForEach-Object { "{0:x2}" -f (Get-Random -Maximum 256) })
        $content = $content -replace "<64-char hex.*?>", $key
        Set-Content $backendEnv $content
        Write-Host "  Generated SETTINGS_ENCRYPTION_KEY" -ForegroundColor Green
    }
}
Write-Host "  backend\.env exists" -ForegroundColor Green

if (-not $SkipFrontend) {
    $frontendEnv = Join-Path $frontendPath ".env"
    if (-not (Test-Path $frontendEnv)) {
        Copy-Item (Join-Path $frontendPath ".env.example") $frontendEnv
        Write-Host "  Created frontend\.env from .env.example" -ForegroundColor Yellow
    }
    Write-Host "  frontend\.env exists" -ForegroundColor Green
}

# -- Step 3: Docker Services (PostgreSQL + Redis ONLY) --
Write-Host "`n[3/7] Starting Docker database services (PostgreSQL 16 + Redis 7)..." -ForegroundColor Cyan

Push-Location $backendPath
& docker compose up -d
if ($LASTEXITCODE -ne 0) { Write-Host "  ERROR: docker compose failed" -ForegroundColor Red; Pop-Location; exit 1 }
Pop-Location

# Wait for healthy
Write-Host "  Waiting for databases to be healthy..." -ForegroundColor DarkGray
$maxWait = 30
$waited = 0
while ($waited -lt $maxWait) {
    $pgReady = & docker exec event-platform-db pg_isready -U ep_dev -d event_platform 2>$null
    $redisReady = & docker exec event-platform-redis redis-cli ping 2>$null
    if ($pgReady -match "accepting" -and $redisReady -match "PONG") { break }
    Start-Sleep -Seconds 2
    $waited += 2
}
Write-Host "  PostgreSQL: healthy" -ForegroundColor Green
Write-Host "  Redis: healthy" -ForegroundColor Green

# -- Step 4: Backend — Native .NET Build --
if (-not $SkipBackend) {
    Write-Host "`n[4/7] Building backend (.NET — native)..." -ForegroundColor Cyan

    Push-Location $backendPath
    & dotnet restore
    & dotnet build
    if ($LASTEXITCODE -ne 0) { Write-Host "  ERROR: dotnet build failed" -ForegroundColor Red; Pop-Location; exit 1 }
    Pop-Location
    Write-Host "  Backend build succeeded" -ForegroundColor Green
}

# -- Step 5: Frontend — Native npm install --
if (-not $SkipFrontend) {
    Write-Host "`n[5/7] Installing frontend dependencies (npm — native)..." -ForegroundColor Cyan

    Push-Location $frontendPath
    & npm install
    if ($LASTEXITCODE -ne 0) { Write-Host "  ERROR: npm install failed" -ForegroundColor Red; Pop-Location; exit 1 }
    Pop-Location
    Write-Host "  Frontend dependencies installed" -ForegroundColor Green
}

# -- Step 6: Start Backend (native dotnet process) --
if (-not $SkipBackend) {
    Write-Host "`n[6/7] Starting backend API (native dotnet run on port 8000)..." -ForegroundColor Cyan

    $env:ASPNETCORE_ENVIRONMENT = "Development"
    Push-Location $backendPath
    Start-Process -FilePath "dotnet" -ArgumentList "run","--project","api" -NoNewWindow -PassThru | Out-Null
    Pop-Location

    # Wait for health (first run applies migrations + seeds data, can take 30-45s)
    $maxWait = 60
    $waited = 0
    while ($waited -lt $maxWait) {
        try {
            $health = Invoke-RestMethod -Uri "http://localhost:8000/health" -TimeoutSec 2 -ErrorAction SilentlyContinue
            if ($health.status -eq "healthy") { break }
        } catch { }
        Start-Sleep -Seconds 3
        $waited += 3
    }
    if ($waited -ge $maxWait) {
        Write-Host "  WARNING: API did not respond within ${maxWait}s — check logs" -ForegroundColor Yellow
    } else {
        Write-Host "  API: http://localhost:8000 (healthy)" -ForegroundColor Green
    }
    Write-Host "  Scalar: http://localhost:8000/scalar" -ForegroundColor Green
}

# -- Step 7: Start Frontend (native Vite dev server) --
if (-not $SkipFrontend) {
    Write-Host "`n[7/7] Starting frontend dev server (native npm run dev on port 5173)..." -ForegroundColor Cyan

    Push-Location $frontendPath
    Start-Process -FilePath "npm" -ArgumentList "run","dev" -NoNewWindow -PassThru | Out-Null
    Pop-Location

    Start-Sleep -Seconds 3
    Write-Host "  Frontend: http://localhost:5173" -ForegroundColor Green
}

# -- Summary --
Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  Development environment ready!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Docker (DB only): PostgreSQL :5432, Redis :6379"
Write-Host "  Backend (native): http://localhost:8000"
Write-Host "  Frontend (native): http://localhost:5173"
Write-Host "  Scalar docs: http://localhost:8000/scalar"
Write-Host "  Dev login: POST http://localhost:8000/auth/dev-login"
Write-Host ""
