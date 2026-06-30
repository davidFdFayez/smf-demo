# Creates a GitHub-ready zip with ALL folders the Dockerfile needs.
# Run:  .\prepare-github-upload.ps1
# Then upload EVERYTHING inside the zip to GitHub repo root (smf-demo).

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$zipPath = "$env:USERPROFILE\Desktop\smf-github-upload.zip"
$staging = "$env:TEMP\smf-github-staging"

if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Path $staging | Out-Null

# Required top-level items for Docker build
$required = @(
    "Dockerfile",
    "SMF.sln",
    "web",
    "src",
    "tests",
    "admin",
    "render.yaml",
    "snapdeploy.json"
)

Write-Host "Copying required project files..."
robocopy $root $staging /E /XD node_modules bin obj .git terminals agent-tools /XF *.zip /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null

Write-Host ""
Write-Host "Checking required paths..."
$missing = @()
foreach ($item in @("Dockerfile", "SMF.sln", "web", "src")) {
    if (-not (Test-Path (Join-Path $staging $item))) { $missing += $item }
}
if ($missing.Count -gt 0) {
    throw "Missing required items: $($missing -join ', ')"
}

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$staging\*" -DestinationPath $zipPath -CompressionLevel Fastest -Force
Remove-Item $staging -Recurse -Force

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host " ZIP READY: $zipPath" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Upload to GitHub (IMPORTANT):"
Write-Host "  1. Extract the zip on your Desktop"
Write-Host "  2. github.com -> your smf-demo repo"
Write-Host "  3. Delete old files if repo only has Dockerfile"
Write-Host "  4. Upload files so repo root looks like:"
Write-Host "       smf-demo/Dockerfile"
Write-Host "       smf-demo/web/"
Write-Host "       smf-demo/src/"
Write-Host "       smf-demo/SMF.sln"
Write-Host ""
Write-Host "  WRONG: smf-demo/smf-github-upload/Dockerfile  (nested folder)"
Write-Host "  RIGHT: smf-demo/Dockerfile  (at repo root)"
Write-Host ""
Write-Host "SnapDeploy: Root Directory = .  |  Dockerfile = Dockerfile  |  Port = 8080"
