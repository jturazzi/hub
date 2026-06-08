<#
.SYNOPSIS
    Build complet de Hub + génération de l'installateur Inno Setup.

.DESCRIPTION
    1. Publie l'application en Release x64
    2. Compile le script Inno Setup → installer\Output\Hub-Setup.exe

.REQUIRES
    - .NET 8 SDK
    - Inno Setup 6  (https://jrsoftware.org/isdl.php)
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Root      = Split-Path $PSScriptRoot -Parent   # dossier Hub/
$IssScript = Join-Path $PSScriptRoot "Hub.iss"

# ── Chemins Inno Setup ───────────────────────────────────────────────
$InnoSetupPaths = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe"
)
$ISCC = $InnoSetupPaths | Where-Object { Test-Path $_ } | Select-Object -First 1

Write-Host ""
Write-Host "═══════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  Hub — Build & Installer" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host ""

# ── 1. Publication Release ───────────────────────────────────────────
Write-Host ""
Write-Host "── Publication Release x64 ──" -ForegroundColor DarkCyan

Push-Location $Root
try {
    dotnet publish Hub.csproj `
        --configuration Release `
        --runtime win-x64 `
        --self-contained false `
        -p:PublishSingleFile=true `
        -p:DebugType=none `
        -p:DebugSymbols=false

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish a échoué (code $LASTEXITCODE)."
    }
}
finally {
    Pop-Location
}

Write-Host "  ✔ Publication terminée." -ForegroundColor Green

# ── 2. Compilation Inno Setup ────────────────────────────────────────
Write-Host ""
Write-Host "── Compilation de l'installateur ──" -ForegroundColor DarkCyan

if (-not $ISCC) {
    Write-Host ""
    Write-Host "  ✖ Inno Setup introuvable." -ForegroundColor Red
    Write-Host "  Téléchargez-le sur : https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    Write-Host "  Puis relancez ce script." -ForegroundColor Yellow
    exit 1
}

Push-Location $PSScriptRoot
try {
    & $ISCC $IssScript
    if ($LASTEXITCODE -ne 0) {
        throw "ISCC a échoué (code $LASTEXITCODE)."
    }
}
finally {
    Pop-Location
}

$output = Join-Path $PSScriptRoot "Output\Hub-Setup.exe"

Write-Host ""
Write-Host "═══════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "  ✔ Installateur prêt !" -ForegroundColor Green
Write-Host "  $output" -ForegroundColor White
Write-Host "═══════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host ""
