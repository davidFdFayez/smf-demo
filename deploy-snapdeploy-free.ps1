# =============================================================================
# FREE CLOUD — NO CREDIT CARD (SnapDeploy)
# =============================================================================
# https://snapdeploy.dev — sign up with GitHub or email only.
# Your PC can be OFF. App auto-sleeps when idle, wakes in ~60 sec on visit.
#
#   .\deploy-snapdeploy-free.ps1
# =============================================================================

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

Clear-Host
Write-Host ""
Write-Host "  ============================================================" -ForegroundColor Cyan
Write-Host "   SMF on SnapDeploy — FREE, NO credit card" -ForegroundColor Cyan
Write-Host "  ============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Works when your PC is OFF." -ForegroundColor Green
Write-Host "  After 15 min idle, first visit takes ~1 min to wake up." -ForegroundColor Yellow
Write-Host ""

# Zip for GitHub (if not already on Desktop)
$zipPath = "$env:USERPROFILE\Desktop\smf-github-upload.zip"
if (-not (Test-Path $zipPath)) {
    Write-Host "  Creating zip on Desktop..." -ForegroundColor Cyan
    $staging = "$env:TEMP\smf-pack"
    if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    robocopy $root $staging /E /XD node_modules bin obj .git terminals agent-tools /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path "$staging\*" -DestinationPath $zipPath -CompressionLevel Fastest -Force
    Remove-Item $staging -Recurse -Force
    Write-Host "  Created: $zipPath" -ForegroundColor Green
} else {
    Write-Host "  Zip already on Desktop: $zipPath" -ForegroundColor Green
}

Write-Host ""
Write-Host "  ============================================================" -ForegroundColor Yellow
Write-Host "   STEPS (no credit card anywhere)" -ForegroundColor Yellow
Write-Host "  ============================================================" -ForegroundColor Yellow
Write-Host ""
Write-Host "  STEP 1 — GitHub (free, no card)"
Write-Host "    https://github.com/signup"
Write-Host "    New repo: https://github.com/new  -> name: smf-demo -> Public"
Write-Host "    Upload files from Desktop zip (extract first, upload contents)"
Write-Host ""
Write-Host "  STEP 2 — SnapDeploy (free, no card)"
Write-Host "    https://snapdeploy.dev/register"
Write-Host "    Sign up with GitHub"
Write-Host ""
Write-Host ""
Write-Host "  ============================================================" -ForegroundColor Red
Write-Host "   Screen: Service Dependencies / Connect add-ons" -ForegroundColor Red
Write-Host "  ============================================================" -ForegroundColor Red
Write-Host ""
Write-Host "  >>> DO NOT add PostgreSQL — this app needs NO database"
Write-Host "  >>> Leave ALL add-on checkboxes UNCHECKED"
Write-Host "  >>> Click: Skip  |  Deploy without add-ons  |  Continue"
Write-Host "  >>> If you already picked a Postgres TEMPLATE, go Back and use"
Write-Host "      GitHub + Dockerfile instead (see Step 3 below)"
Write-Host ""
Write-Host "  STEP 3 — SnapDeploy container settings (COPY EXACTLY)"
Write-Host ""
Write-Host "    +---------------------------+----------------------------------+"
Write-Host "    | Field                     | Value                            |"
Write-Host "    +---------------------------+----------------------------------+"
Write-Host "    | Root Directory            |  .                               |"
Write-Host "    | Dockerfile path           |  Dockerfile                      |"
Write-Host "    | Build context             |  .                               |"
Write-Host "    | Port                      |  8080                            |"
Write-Host "    +---------------------------+----------------------------------+"
Write-Host ""
Write-Host "    WRONG (causes build fail):"
Write-Host "      E:\New folder (2)\deploy\Dockerfile.all-in-one"
Write-Host "    RIGHT (GitHub repo paths only):"
Write-Host "      Dockerfile"
Write-Host ""
Write-Host "    IMPORTANT — PostgreSQL add-on:"
Write-Host "    >>> DO NOT select PostgreSQL (or any database add-on)"
Write-Host "    >>> Click Skip / Continue without add-ons / Deploy with none selected"
Write-Host "    >>> This app uses built-in in-memory demo data — no external DB"
Write-Host ""
Write-Host "    Deploy"
Write-Host ""
Write-Host "  STEP 4 — Deploy admin (optional second container)"
Write-Host "    Create New Container -> GitHub -> smf-demo"
Write-Host "    Dockerfile: admin/Dockerfile.render"
Write-Host "    Context: admin"
Write-Host "    Port: 80"
Write-Host "    Env: API_UPSTREAM = your smf-api SnapDeploy URL host:port"
Write-Host "    (e.g. smf-api-xxxxx.snapdeploy.dev:443 or internal URL from dashboard)"
Write-Host ""
Write-Host "  You get URLs like:"
Write-Host "    https://your-container.snapdeploy.dev/watch?match=match-002"
Write-Host ""
Write-Host "  ============================================================" -ForegroundColor Green
Write-Host ""

$open = Read-Host "  Open GitHub + SnapDeploy signup in browser? (y/n)"
if ($open -eq 'y' -or $open -eq 'Y') {
    Start-Process "https://github.com/signup"
    Start-Sleep -Seconds 1
    Start-Process "https://github.com/new"
    Start-Sleep -Seconds 1
    Start-Process "https://snapdeploy.dev/register"
}

Write-Host ""
Write-Host "  SnapDeploy docs: https://snapdeploy.dev/docs/getting-started" -ForegroundColor DarkGray
