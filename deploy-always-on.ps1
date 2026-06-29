# Deploy SMF to a FREE cloud server — works when your PC is OFF forever.
#
# Run:  .\deploy-always-on.ps1

param(
    [string] $ServerHost = "",
    [string] $ServerUser = "ubuntu",
    [string] $SshKeyPath = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " SMF — Always-on cloud deploy" -ForegroundColor Cyan
Write-Host " Works when your PC is CLOSED" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if (-not $ServerHost) {
    Write-Host "ONE-TIME SETUP (free Oracle Cloud VM, `$0/month):" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  1. https://cloud.oracle.com/free  → Sign up"
    Write-Host "  2. Compute → Instances → Create instance"
    Write-Host "  3. Ubuntu 22.04/24.04, Ampere A1 (Always Free)"
    Write-Host "  4. Save the .pem SSH key"
    Write-Host "  5. Security List → open TCP ports 22, 80, 443"
    Write-Host "  6. Copy the PUBLIC IP"
    Write-Host ""
    $ServerHost = Read-Host "Paste your VM PUBLIC IP"
}

if (-not $SshKeyPath) {
    $input = Read-Host "Path to .pem SSH key (full path)"
    if ($input -and (Test-Path $input)) { $SshKeyPath = $input }
}

$params = @{
    ServerHost = $ServerHost.Trim()
    ServerUser = $ServerUser
}
if ($SshKeyPath) { $params.SshKeyPath = $SshKeyPath }

& "$root\publish-global.ps1" @params

$sslip = $ServerHost.Trim().Replace('.', '-') + ".sslip.io"
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " PERMANENT URLs (PC OFF = still works)" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Website:  https://${sslip}/"
Write-Host "  Watch:    https://${sslip}/watch?match=match-002"
Write-Host "  Admin:    https://admin.${sslip}/"
Write-Host "========================================" -ForegroundColor Green
