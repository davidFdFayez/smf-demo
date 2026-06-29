# Keeps public demo URLs stable across restarts.
# Run from repo root:  .\start-demo-tunnels.ps1
#
# Fixed URLs (localtunnel — same subdomain every time you restart):
#   Web:   https://smf-saudi-web.loca.lt
#   Admin: https://smf-saudi-admin.loca.lt
#
# First visit per IP shows a one-time "Continue" page; enter the IP shown on screen.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "Checking Docker stack..."
Push-Location $root
docker compose ps
if ($LASTEXITCODE -ne 0) {
    Write-Host "Starting Docker stack..."
    docker compose up -d
}
Pop-Location

Write-Host ""
Write-Host "Starting stable public tunnels..."
Write-Host "  Web:   https://smf-saudi-web.loca.lt"
Write-Host "  Admin: https://smf-saudi-admin.loca.lt"
Write-Host ""
Write-Host "Keep this window open while your boss is testing."
Write-Host ""

Start-Process -NoNewWindow -FilePath "npx" -ArgumentList @("--yes", "localtunnel", "--port", "8080", "--subdomain", "smf-saudi-web")
Start-Sleep -Seconds 2
Start-Process -NoNewWindow -FilePath "npx" -ArgumentList @("--yes", "localtunnel", "--port", "8081", "--subdomain", "smf-saudi-admin")

Write-Host "Tunnels started. Press Ctrl+C to stop (or close this window)."
while ($true) { Start-Sleep -Seconds 3600 }
