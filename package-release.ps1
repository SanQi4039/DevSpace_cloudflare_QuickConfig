param(
    [string]$Version = '0.1.0',
    [switch]$Offline
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
$packageName = "DevSpaceQuickTunnelTray-v$Version-win-x64"
$stage = Join-Path $dist $packageName
$zip = Join-Path $dist "$packageName.zip"

$sourceText = Get-Content (Join-Path $root 'DevSpaceQuickTunnelTray.cs') -Raw
if ($sourceText -notmatch 'EnvironmentVariables\["DEVSPACE_SUBAGENTS"\]\s*=\s*"0"') {
    throw 'Release blocked: DEVSPACE_SUBAGENTS=0 mitigation is no longer enforced. Re-audit required.'
}

& (Join-Path $root 'build.ps1')

New-Item -ItemType Directory -Force $dist | Out-Null
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $zip -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stage | Out-Null

foreach ($file in @(
    'DevSpaceQuickTunnelTray.exe',
    'DevSpaceQuickTunnelTray.cs',
    'build.ps1',
    'setup-runtime.ps1',
    'audit-runtime.ps1',
    'package.json',
    'settings.example.json',
    'README.md',
    'LICENSE',
    'DOWNLOADS.md',
    'SECURITY_AUDIT.md',
    'THIRD_PARTY_NOTICES.md'
)) {
    Copy-Item (Join-Path $root $file) $stage
}

if ($Offline) {
    & (Join-Path $stage 'setup-runtime.ps1') -Force

    $licenseDir = Join-Path $stage 'third-party-licenses'
    New-Item -ItemType Directory -Force $licenseDir | Out-Null
    Copy-Item `
        (Join-Path $stage 'runtime\node-v22.22.3-win-x64\LICENSE') `
        (Join-Path $licenseDir 'Node.js-LICENSE.txt')
    Copy-Item `
        (Join-Path $stage 'runtime\devspace\node_modules\@waishnav\devspace\LICENSE') `
        (Join-Path $licenseDir 'DevSpace-LICENSE.txt')

    & curl.exe -L --fail --retry 3 `
        --output (Join-Path $licenseDir 'cloudflared-LICENSE.txt') `
        'https://raw.githubusercontent.com/cloudflare/cloudflared/2026.8.2/LICENSE'
    if ($LASTEXITCODE -ne 0) { throw 'Failed to download cloudflared license text.' }
}

Push-Location $dist
try {
    & tar.exe -a -c -f (Split-Path $zip -Leaf) $packageName
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}

Write-Host $(if ($Offline) { "Created offline release $zip" } else { "Created lightweight release $zip" })
