# =============================================================================
# DEPLOY SMF TO THE CLOUD — works when your PC is OFF (no local Docker needed)
# =============================================================================
#
# Your PC is only used ONCE to upload files. After that, the cloud server runs
# 24/7 without your computer.
#
#   .\deploy-cloud.ps1
#
# FREE option: Oracle Cloud Always Free (~10 min one-time setup)
# =============================================================================

param(
    [string] $ServerHost = "",
    [string] $SshKeyPath = "",
    [string] $ServerUser = "ubuntu"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

Clear-Host
Write-Host ""
Write-Host "  ============================================================" -ForegroundColor Cyan
Write-Host "   SMF CLOUD DEPLOY — PC can be OFF forever after this" -ForegroundColor Cyan
Write-Host "  ============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  You do NOT need Docker running on this PC." -ForegroundColor Green
Write-Host "  This uploads the app to a free cloud server (runs 24/7)." -ForegroundColor Green
Write-Host ""

if (-not $ServerHost) {
    Write-Host "  STEP 1 — Create free Oracle Cloud VM (one time):" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "    a) Open:  https://cloud.oracle.com/free"
    Write-Host "    b) Sign up (credit card for verification, Always Free = `$0)"
    Write-Host "    c) Menu (top left) -> Compute -> Instances -> Create instance"
    Write-Host "    d) Name: smf-demo"
    Write-Host "    e) Image: Ubuntu 22.04 or 24.04"
    Write-Host "    f) Shape: Change shape -> Ampere -> VM.Standard.A1.Flex"
    Write-Host "       Set 2 OCPU, 12 GB RAM (still free)"
    Write-Host "    g) Add SSH key -> Generate key pair -> DOWNLOAD the .pem file"
    Write-Host "    h) Networking -> Assign public IPv4 address: Yes"
    Write-Host "    i) Before Create, open your VCN Security List and add INGRESS:"
    Write-Host "         TCP 22 (SSH), TCP 80 (HTTP), TCP 443 (HTTPS)"
    Write-Host "    j) Click Create -> wait Running -> copy PUBLIC IP"
    Write-Host ""
    Write-Host "  STEP 2 — Paste details below:" -ForegroundColor Yellow
    Write-Host ""
    $ServerHost = Read-Host "  VM Public IP (e.g. 123.45.67.89)"
}

if (-not $SshKeyPath) {
    Write-Host ""
    $SshKeyPath = Read-Host "  Full path to .pem file (e.g. C:\Users\You\Downloads\ssh-key-2026.pem)"
}

if (-not (Test-Path $SshKeyPath)) {
    throw "SSH key not found: $SshKeyPath"
}

# Oracle Ubuntu images use 'ubuntu' user
Write-Host ""
Write-Host "  Uploading to cloud server (first time takes 10-15 min)..." -ForegroundColor Cyan
Write-Host ""

& "$root\publish-global.ps1" -ServerHost $ServerHost.Trim() -ServerUser $ServerUser -SshKeyPath $SshKeyPath

$sslip = $ServerHost.Trim().Replace('.', '-') + ".sslip.io"

Write-Host ""
Write-Host "  ============================================================" -ForegroundColor Green
Write-Host "   SUCCESS — close your PC, URLs keep working" -ForegroundColor Green
Write-Host "  ============================================================" -ForegroundColor Green
Write-Host ""
Write-Host "   Website:  https://${sslip}/" -ForegroundColor White
Write-Host "   Watch:    https://${sslip}/watch?match=match-002" -ForegroundColor White
Write-Host "   Admin:    https://admin.${sslip}/" -ForegroundColor White
Write-Host ""
Write-Host "  Save these URLs. They work 24/7 from the cloud server." -ForegroundColor Green
Write-Host "  You can close Docker Desktop and shut down your PC." -ForegroundColor Green
Write-Host "  ============================================================" -ForegroundColor Green
Write-Host ""

# Save URLs for later
@(
    "PUBLIC_WEB=https://${sslip}/"
    "PUBLIC_WATCH=https://${sslip}/watch?match=match-002"
    "PUBLIC_ADMIN=https://admin.${sslip}/"
    "DEPLOYED=$(Get-Date -Format o)"
) | Set-Content -Path "$root\cloud-urls.txt"

Write-Host "  URLs also saved to: cloud-urls.txt" -ForegroundColor DarkGray
