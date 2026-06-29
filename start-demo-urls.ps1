# Start Docker + print local URLs. Run after opening Docker Desktop.
# Usage:  .\start-demo-urls.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$cf = "C:\Program Files (x86)\cloudflared\cloudflared.exe"

Write-Host "Starting SMF Docker stack..."
Push-Location $root
docker compose up -d
if ($LASTEXITCODE -ne 0) {
    Write-Host "Docker is not running. Open Docker Desktop, wait until ready, run again." -ForegroundColor Red
    exit 1
}
Pop-Location

for ($i = 0; $i -lt 24; $i++) {
    try {
        if ((Invoke-WebRequest -Uri "http://127.0.0.1:8080" -TimeoutSec 5 -UseBasicParsing).StatusCode -eq 200) { break }
    } catch { Start-Sleep -Seconds 5 }
}

Get-CimInstance Win32_Process -Filter "Name='cloudflared.exe'" |
    Where-Object { $_.CommandLine -match 'tunnel --url' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

Write-Host "Starting public HTTPS tunnels (wait ~10s)..."
$webOut = Join-Path $env:TEMP "smf-web-tunnel.log"
$adminOut = Join-Path $env:TEMP "smf-admin-tunnel.log"
Start-Process -WindowStyle Hidden -FilePath $cf -ArgumentList "tunnel","--url","http://127.0.0.1:8080" -RedirectStandardError $webOut
Start-Sleep -Seconds 2
Start-Process -WindowStyle Hidden -FilePath $cf -ArgumentList "tunnel","--url","http://127.0.0.1:8081" -RedirectStandardError $adminOut
Start-Sleep -Seconds 10

$webUrl = [regex]::Match((Get-Content $webOut -Raw -ErrorAction SilentlyContinue), 'https://[a-z0-9-]+\.trycloudflare\.com').Value
$adminUrl = [regex]::Match((Get-Content $adminOut -Raw -ErrorAction SilentlyContinue), 'https://[a-z0-9-]+\.trycloudflare\.com').Value

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host " SMF is running" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host "  Local web:   http://localhost:8080"
Write-Host "  Local admin: http://localhost:8081"
if ($webUrl) {
    Write-Host "  Public web:   $webUrl"
    Write-Host "  Watch:        $webUrl/watch?match=match-002"
}
if ($adminUrl) { Write-Host "  Public admin: $adminUrl" }
Write-Host "============================================" -ForegroundColor Green
Write-Host "Keep Docker Desktop running. For 24/7 URLs: .\deploy-always-on.ps1" -ForegroundColor Yellow
