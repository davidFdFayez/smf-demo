# Configure Cloudflare Tunnel public hostnames for SMF web + admin.
# Requires a Cloudflare API token with: Tunnel Edit + DNS Edit
# Create at: https://dash.cloudflare.com/profile/api-tokens
#
# Usage:
#   .\configure-cloudflare-tunnel.ps1 -ApiToken "..." -Domain "yourdomain.com"
#
# Creates:
#   https://smf-web.yourdomain.com      → website / watch
#   https://smf-admin.yourdomain.com    → admin panel

param(
    [Parameter(Mandatory = $true)]
    [string] $ApiToken,

    [Parameter(Mandatory = $true)]
    [string] $Domain,

    [string] $AccountId = "38d7e28ccf6668ec5414c0850bb97492",
    [string] $TunnelId = "01a75c55-811c-4350-8c35-72d8b96716af"
)

$ErrorActionPreference = "Stop"
$headers = @{
    Authorization = "Bearer $ApiToken"
    "Content-Type" = "application/json"
}

Write-Host "Looking up zone for $Domain..."
$zones = Invoke-RestMethod -Uri "https://api.cloudflare.com/client/v4/zones?name=$Domain" -Headers $headers
if (-not $zones.success -or $zones.result.Count -eq 0) {
    throw "Zone '$Domain' not found on Cloudflare. Add the domain to your account first."
}
$zoneId = $zones.result[0].id
Write-Host "Zone ID: $zoneId"

$ingressBody = @{
    config = @{
        ingress = @(
            @{
                hostname = "smf-web.$Domain"
                service  = "http://web:80"
            },
            @{
                hostname = "smf-admin.$Domain"
                service  = "http://admin:80"
            },
            @{
                service = "http_status:404"
            }
        )
    }
} | ConvertTo-Json -Depth 6

Write-Host "Updating tunnel ingress routes..."
$cfg = Invoke-RestMethod -Method PUT `
    -Uri "https://api.cloudflare.com/client/v4/accounts/$AccountId/cfd_tunnel/$TunnelId/configurations" `
    -Headers $headers -Body $ingressBody
if (-not $cfg.success) { throw "Tunnel config failed: $($cfg.errors | ConvertTo-Json)" }

$cnameTarget = "$TunnelId.cfargotunnel.com"
foreach ($sub in @("smf-web", "smf-admin")) {
    $name = "$sub.$Domain"
    Write-Host "Creating DNS CNAME: $name -> $cnameTarget"
    $dnsBody = @{
        type    = "CNAME"
        name    = $sub
        content = $cnameTarget
        proxied = $true
    } | ConvertTo-Json
    $dns = Invoke-RestMethod -Method POST `
        -Uri "https://api.cloudflare.com/client/v4/zones/$zoneId/dns_records" `
        -Headers $headers -Body $dnsBody
    if (-not $dns.success) {
        Write-Warning "DNS for $name may already exist — check Cloudflare dashboard."
    }
}

# Update local .env CORS origins and restart API
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$envFile = Join-Path $root ".env"
$webOrigin = "https://smf-web.$Domain"
$adminOrigin = "https://smf-admin.$Domain"
$envContent = Get-Content $envFile -Raw -ErrorAction SilentlyContinue
if ($envContent -match "PUBLIC_WEB_ORIGIN=") {
    $envContent = $envContent -replace "PUBLIC_WEB_ORIGIN=.*", "PUBLIC_WEB_ORIGIN=$webOrigin"
} else {
    $envContent += "`nPUBLIC_WEB_ORIGIN=$webOrigin"
}
if ($envContent -match "PUBLIC_ADMIN_ORIGIN=") {
    $envContent = $envContent -replace "PUBLIC_ADMIN_ORIGIN=.*", "PUBLIC_ADMIN_ORIGIN=$adminOrigin"
} else {
    $envContent += "`nPUBLIC_ADMIN_ORIGIN=$adminOrigin"
}
Set-Content -Path $envFile -Value $envContent.Trim()

Write-Host "Restarting API with updated CORS..."
Push-Location $root
docker compose --env-file .env up -d api
Pop-Location

Write-Host ""
Write-Host "Published URLs:" -ForegroundColor Green
Write-Host "  Website:  $webOrigin/"
Write-Host "  Watch:    $webOrigin/watch?match=match-002"
Write-Host "  Admin:    $adminOrigin/"
