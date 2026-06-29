# =============================================================================
# FREE CLOUD HOSTING — NO CREDIT CARD (Render.com)
# =============================================================================
# Your PC can be OFF. Render hosts the app (free tier).
#
# Trade-off: after 15 min idle, first visit takes ~30-60 sec to wake up.
# No credit card required — only a free GitHub account.
#
#   .\deploy-render-free.ps1
# =============================================================================

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

Clear-Host
Write-Host ""
Write-Host "  ============================================================" -ForegroundColor Cyan
Write-Host "   SMF on Render — FREE, no credit card" -ForegroundColor Cyan
Write-Host "  ============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Your PC can be closed. Render runs the app in the cloud." -ForegroundColor Green
Write-Host "  First visit after idle may take ~1 minute (free tier wake-up)." -ForegroundColor Yellow
Write-Host ""

# Create upload zip (exclude heavy folders)
$zipPath = Join-Path $env:USERPROFILE "Desktop\smf-github-upload.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Write-Host "  Creating upload zip on your Desktop..." -ForegroundColor Cyan
$staging = Join-Path $env:TEMP "smf-render-$(Get-Date -Format 'yyyyMMddHHmmss')"
New-Item -ItemType Directory -Path $staging | Out-Null
$exclude = @("node_modules", "bin", "obj", ".git", "terminals", "agent-tools")
Get-ChildItem -Path $root -Force | Where-Object { $exclude -notcontains $_.Name } |
    ForEach-Object { Copy-Item -Path $_.FullName -Destination $staging -Recurse -Force }
Compress-Archive -Path "$staging\*" -DestinationPath $zipPath -Force
Remove-Item -Recurse -Force $staging

Write-Host "  Zip ready: $zipPath" -ForegroundColor Green
Write-Host ""
Write-Host "  ============================================================" -ForegroundColor Yellow
Write-Host "   FOLLOW THESE 5 STEPS (one time, ~15 minutes)" -ForegroundColor Yellow
Write-Host "  ============================================================" -ForegroundColor Yellow
Write-Host ""
Write-Host "  STEP 1 — Free GitHub account (no card)"
Write-Host "    https://github.com/signup"
Write-Host ""
Write-Host "  STEP 2 — New repository"
Write-Host "    https://github.com/new"
Write-Host "    Name: smf-demo  |  Public  |  Create repository"
Write-Host ""
Write-Host "  STEP 3 — Upload code"
Write-Host "    On the new repo page: Add file -> Upload files"
Write-Host "    Drag ALL files from the zip on your Desktop"
Write-Host "    Commit changes"
Write-Host ""
Write-Host "  STEP 4 — Free Render account (no card)"
Write-Host "    https://dashboard.render.com/register"
Write-Host "    Sign up with GitHub (not credit card)"
Write-Host ""
Write-Host "  STEP 5 — Deploy"
Write-Host "    Render Dashboard -> New + -> Blueprint"
Write-Host "    Connect your smf-demo GitHub repo"
Write-Host "    Render reads render.yaml and creates 3 services"
Write-Host "    Wait ~15 min for first build"
Write-Host ""
Write-Host "  You will get permanent URLs like:"
Write-Host "    https://smf-web.onrender.com/watch?match=match-002"
Write-Host "    https://smf-admin.onrender.com/"
Write-Host ""
Write-Host "  ============================================================" -ForegroundColor Green
Write-Host ""

$open = Read-Host "  Open GitHub signup + Render register in browser now? (y/n)"
if ($open -eq 'y' -or $open -eq 'Y') {
    Start-Process "https://github.com/signup"
    Start-Sleep -Seconds 2
    Start-Process "https://github.com/new"
    Start-Sleep -Seconds 2
    Start-Process "https://dashboard.render.com/register"
}

Write-Host ""
Write-Host "  When Render finishes, save your URLs to cloud-urls.txt" -ForegroundColor DarkGray
Write-Host "  Need help on a step? Tell me which step you're on." -ForegroundColor DarkGray
