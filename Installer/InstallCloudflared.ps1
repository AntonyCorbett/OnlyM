# Installs cloudflared (Cloudflare Quick Tunnel client) for OnlyM's internet remote control.
# Tries winget first; if that is unavailable or fails, downloads the official binary into OnlyM's folder.
# -Scope user installs without admin (winget per-user, or download into -AppDir).
param([string]$AppDir, [ValidateSet('machine', 'user')][string]$Scope = 'machine')
$ErrorActionPreference = 'Continue'
$log = Join-Path $env:TEMP 'OnlyM-cloudflared-install.log'
function Log($m) { Add-Content -Path $log -Value ("{0}  {1}" -f (Get-Date -Format s), $m) }

Log "Install requested (scope $Scope, AppDir $AppDir)"
if (Get-Command cloudflared -ErrorAction SilentlyContinue) { Log 'cloudflared already on PATH'; exit 0 }

$winget = Get-Command winget -ErrorAction SilentlyContinue
if ($winget) {
    Log 'Installing via winget'
    & $winget.Source install --id Cloudflare.cloudflared -e --silent --scope $Scope --accept-package-agreements --accept-source-agreements *>> $log
    if ($LASTEXITCODE -eq 0) { Log 'winget install succeeded'; exit 0 }
    Log "winget failed with exit code $LASTEXITCODE"
}

try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $arch = if ([Environment]::Is64BitOperatingSystem) { 'amd64' } else { '386' }
    $url = "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-$arch.exe"
    if (-not $AppDir) { throw 'No -AppDir given for the download fallback' }
    New-Item -ItemType Directory -Force -Path $AppDir | Out-Null
    $dest = Join-Path $AppDir 'cloudflared.exe'
    Log "Downloading $url to $dest"
    Invoke-WebRequest -Uri $url -OutFile $dest -UseBasicParsing
    Log 'Download succeeded'
    exit 0
}
catch {
    Log "Download failed: $_"
    exit 1
}