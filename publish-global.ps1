# Publish SMF to a cloud server — 24/7, works when your PC is off.
#
# Usage:
#   .\publish-global.ps1 -ServerHost 203.0.113.10 -SshKeyPath C:\keys\oracle.pem

param(
    [Parameter(Mandatory = $true)]
    [string] $ServerHost,

    [string] $ServerUser = "ubuntu",
    [string] $RemoteDir = "/opt/smf",
    [string] $SshKeyPath = "",
    [int] $SshPort = 22
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

$sshOpts = @("-p", $SshPort, "-o", "StrictHostKeyChecking=no")
$scpOpts = @("-P", $SshPort, "-o", "StrictHostKeyChecking=no")
if ($SshKeyPath -and (Test-Path $SshKeyPath)) {
    $sshOpts = @("-i", $SshKeyPath) + $sshOpts
    $scpOpts = @("-i", $SshKeyPath) + $scpOpts
}

Write-Host ""
Write-Host "=== SMF cloud publish ===" -ForegroundColor Cyan
Write-Host "Target: ${ServerUser}@${ServerHost}:${RemoteDir}"
Write-Host ""

if (-not (Get-Command ssh -ErrorAction SilentlyContinue)) {
    throw "OpenSSH not found. Install 'OpenSSH Client' from Windows Optional Features."
}

$staging = Join-Path $env:TEMP "smf-deploy-$(Get-Date -Format 'yyyyMMddHHmmss')"
New-Item -ItemType Directory -Path $staging | Out-Null

Write-Host "Staging project files..."
$exclude = @("node_modules", "bin", "obj", ".git", "terminals")
Get-ChildItem -Path $root -Force | Where-Object { $exclude -notcontains $_.Name } |
    ForEach-Object { Copy-Item -Path $_.FullName -Destination $staging -Recurse -Force }

$archive = "$staging.zip"
if (Test-Path $archive) { Remove-Item $archive -Force }
Compress-Archive -Path "$staging\*" -DestinationPath $archive

Write-Host "Uploading to server..."
ssh @sshOpts "${ServerUser}@${ServerHost}" "sudo mkdir -p $RemoteDir && sudo chown -R ${ServerUser}:${ServerUser} $RemoteDir"
scp @scpOpts $archive "${ServerUser}@${ServerHost}:/tmp/smf-deploy.zip"

Write-Host "Installing on server (first build takes 10-15 min)..."
$remoteScript = @"
set -e
sudo mkdir -p $RemoteDir
sudo chown -R ${ServerUser}:${ServerUser} $RemoteDir
cd $RemoteDir
unzip -o /tmp/smf-deploy.zip -d $RemoteDir
chmod +x deploy/bootstrap-vps.sh
sudo bash deploy/bootstrap-vps.sh $RemoteDir
"@

ssh @sshOpts "${ServerUser}@${ServerHost}" $remoteScript

Remove-Item -Recurse -Force $staging, $archive -ErrorAction SilentlyContinue

$sslip = $ServerHost.Replace('.', '-') + ".sslip.io"
Write-Host ""
Write-Host "Deploy complete." -ForegroundColor Green
Write-Host "  Website:  https://${sslip}/"
Write-Host "  Watch:    https://${sslip}/watch?match=match-002"
Write-Host "  Admin:    https://admin.${sslip}/"
