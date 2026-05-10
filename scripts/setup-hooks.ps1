
$ErrorActionPreference = "Stop"

function Require-Cmd {
    param([string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        Write-Error "$Name is not on PATH. Install it and re-run."
        exit 1
    }
}

Require-Cmd "pre-commit"

Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    pre-commit install
    if ($LASTEXITCODE -ne 0) { throw "pre-commit install failed" }

    pre-commit install --hook-type commit-msg
    if ($LASTEXITCODE -ne 0) { throw "pre-commit install --hook-type commit-msg failed" }

    if (-not (Test-Path ".secrets.baseline")) {
        Write-Host "Generating initial detect-secrets baseline..."
        detect-secrets scan > .secrets.baseline
    }

    Write-Host "Hooks installed. Commits will now run gitleaks + detect-secrets."
}
finally {
    Pop-Location
}
