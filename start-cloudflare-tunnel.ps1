# Starts the always-on Cloudflare named tunnel (web + admin) alongside Docker.
# Run from repo root:  .\start-cloudflare-tunnel.ps1
#
# In Cloudflare Zero Trust (one.dash.cloudflare.com) → Networks → Tunnels → your tunnel
# add TWO public hostnames:
#   Service URL 1:  http://web:80      (website / watch page)
#   Service URL 2:  http://admin:80    (admin panel)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$envFile = Join-Path $root ".env"

if (-not (Test-Path $envFile)) {
    Write-Error "Missing .env — copy .env.cloud.example and set CLOUDFLARE_TUNNEL_TOKEN."
}

Write-Host "Starting Docker stack + Cloudflare tunnel..."
Push-Location $root
docker compose --env-file .env up -d
docker compose --env-file .env --profile tunnel up -d cloudflared
Pop-Location

Write-Host ""
Write-Host "Tunnel container logs (public hostnames appear in Cloudflare dashboard):"
Start-Sleep -Seconds 3
docker logs smf-tunnel --tail 20 2>&1

Write-Host ""
Write-Host "Configure public URLs in Cloudflare if not done yet:"
Write-Host "  https://one.dash.cloudflare.com → Networks → Tunnels → Public Hostname"
Write-Host "  Web:   http://web:80"
Write-Host "  Admin: http://admin:80"
