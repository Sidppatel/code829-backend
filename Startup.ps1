# Startup.ps1 — Start the full development environment
param(
    [switch]$SkipFrontend,
    [switch]$SkipBackend
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$backendPath = Join-Path $root "backend"
$frontendPath = Join-Path $root "frontend"

# Step 1: Prerequisites
Write-Host ""
Write-Host "[1/7] Checking prerequisites..." -ForegroundColor Cyan
$dv = & dotnet --version 2>$null
if (-not $dv) { Write-Host "  ERROR: dotnet not found" -ForegroundColor Red; exit 1 }
Write-Host "  dotnet: $dv" -ForegroundColor Green
$dk = & docker --version 2>$null
if (-not $dk) { Write-Host "  ERROR: Docker not found" -ForegroundColor Red; exit 1 }
Write-Host "  Docker: $dk" -ForegroundColor Green
if (-not $SkipFrontend) {
    $nv = & node --version 2>$null
    if (-not $nv) { Write-Host "  ERROR: Node not found" -ForegroundColor Red; exit 1 }
    Write-Host "  Node: $nv" -ForegroundColor Green
}

# Step 2: Environment files
Write-Host ""
Write-Host "[2/7] Checking environment files..." -ForegroundColor Cyan
$beEnv = Join-Path $backendPath ".env"
if (-not (Test-Path $beEnv)) {
    Copy-Item (Join-Path $backendPath ".env.example") $beEnv
    $c = Get-Content $beEnv -Raw
    if ($c -match "<64-char hex") {
        $key = -join ((1..32) | ForEach-Object { "{0:x2}" -f (Get-Random -Maximum 256) })
        $c = $c -replace "<64-char hex.*?>", $key
        Set-Content $beEnv $c
        Write-Host "  Generated SETTINGS_ENCRYPTION_KEY" -ForegroundColor Green
    }
}
Write-Host "  backend .env OK" -ForegroundColor Green
if (-not $SkipFrontend) {
    $feEnv = Join-Path $frontendPath ".env"
    if (-not (Test-Path $feEnv)) { Copy-Item (Join-Path $frontendPath ".env.example") $feEnv }
    Write-Host "  frontend .env OK" -ForegroundColor Green
}

# Step 3: Docker DB (PostgreSQL + Redis only)
Write-Host ""
Write-Host "[3/7] Starting Docker DB services..." -ForegroundColor Cyan
Push-Location $backendPath
& docker compose up -d
if ($LASTEXITCODE -ne 0) { Write-Host "  ERROR: docker compose failed" -ForegroundColor Red; Pop-Location; exit 1 }
Pop-Location
Write-Host "  Waiting for databases to initialize..." -ForegroundColor DarkGray
Start-Sleep -Seconds 10
Write-Host "  PostgreSQL: ready (port 5432)" -ForegroundColor Green
Write-Host "  Redis: ready (port 6379)" -ForegroundColor Green

# Step 4: Backend build
if (-not $SkipBackend) {
    Write-Host ""
    Write-Host "[4/7] Building backend (.NET native)..." -ForegroundColor Cyan
    Push-Location $backendPath
    & dotnet restore
    & dotnet build
    if ($LASTEXITCODE -ne 0) { Write-Host "  ERROR: build failed" -ForegroundColor Red; Pop-Location; exit 1 }
    Pop-Location
    Write-Host "  Build succeeded" -ForegroundColor Green
}

# Step 5: Frontend install
if (-not $SkipFrontend) {
    Write-Host ""
    Write-Host "[5/7] Installing frontend deps (npm native)..." -ForegroundColor Cyan
    Push-Location $frontendPath
    & npm install
    if ($LASTEXITCODE -ne 0) { Write-Host "  ERROR: npm install failed" -ForegroundColor Red; Pop-Location; exit 1 }
    Pop-Location
    Write-Host "  Dependencies installed" -ForegroundColor Green
}

# Step 6: Start backend (hidden background process)
if (-not $SkipBackend) {
    Write-Host ""
    Write-Host "[6/7] Starting backend API on port 8000..." -ForegroundColor Cyan
    $apiLog = Join-Path $backendPath "api.log"
    $apiErr = Join-Path $backendPath "api.err.log"
    Start-Process -FilePath (Join-Path $backendPath "start-api.cmd") -WorkingDirectory $backendPath -RedirectStandardOutput $apiLog -RedirectStandardError $apiErr -WindowStyle Hidden
    Write-Host "  Waiting for API (first run: migrations + seeding)..." -ForegroundColor DarkGray
    $w = 0
    $ready = $false
    while ($w -lt 120) {
        try {
            $h = Invoke-RestMethod -Uri "http://localhost:8000/health" -TimeoutSec 2 -ErrorAction SilentlyContinue
            if ($h.status -eq "healthy") { $ready = $true; break }
        } catch {}
        Start-Sleep -Seconds 3
        $w += 3
    }
    if ($ready) {
        Write-Host ("  API ready in " + $w + "s") -ForegroundColor Green
    } else {
        Write-Host "  WARNING: API not ready in 120s" -ForegroundColor Yellow
        Write-Host ("  Check log: " + $apiLog) -ForegroundColor Yellow
    }
    Write-Host "  API:    http://localhost:8000" -ForegroundColor Green
    Write-Host "  Scalar: http://localhost:8000/scalar" -ForegroundColor Green
    Write-Host ("  Log: " + $apiLog) -ForegroundColor DarkGray
}

# Step 7: Start frontend (hidden background process)
if (-not $SkipFrontend) {
    Write-Host ""
    Write-Host "[7/7] Starting frontend on port 5173..." -ForegroundColor Cyan
    $feLog = Join-Path $frontendPath "vite.log"
    $feErr = Join-Path $frontendPath "vite.err.log"
    Start-Process -FilePath "npm" -ArgumentList "run","dev" -WorkingDirectory $frontendPath -RedirectStandardOutput $feLog -RedirectStandardError $feErr -WindowStyle Hidden
    Start-Sleep -Seconds 3
    Write-Host "  Frontend: http://localhost:5173" -ForegroundColor Green
    Write-Host ("  Log: " + $feLog) -ForegroundColor DarkGray
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Development environment ready!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Docker (DB): PostgreSQL :5432, Redis :6379"
Write-Host "  Backend:     http://localhost:8000"
Write-Host "  Frontend:    http://localhost:5173"
Write-Host "  Scalar:      http://localhost:8000/scalar"
Write-Host ""
