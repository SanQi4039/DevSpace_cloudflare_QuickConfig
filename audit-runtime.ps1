param(
    [string]$DevSpaceDir = (Join-Path $PSScriptRoot 'runtime\devspace'),
    [string]$NpmPath = (Join-Path $PSScriptRoot 'runtime\node-v22.22.3-win-x64\npm.cmd')
)

$ErrorActionPreference = 'Stop'

$expectedVersions = @{
    '@waishnav/devspace' = '1.0.8'
    '@earendil-works/pi-coding-agent' = '0.80.10'
    'undici' = '8.5.0'
    'brace-expansion' = '5.0.6'
    'protobufjs' = '7.6.4'
}
$knownVulnerablePackages = @(
    '@waishnav/devspace',
    '@earendil-works/pi-coding-agent',
    'undici',
    'brace-expansion',
    'protobufjs'
)
$knownAdvisoryUrls = @(
    'https://github.com/advisories/GHSA-3jxr-9vmj-r5cp',
    'https://github.com/advisories/GHSA-4cwx-7wf7-3272',
    'https://github.com/advisories/GHSA-8xcm-r25x-g524',
    'https://github.com/advisories/GHSA-j3f2-48v5-ccww',
    'https://github.com/advisories/GHSA-jr45-8vmc-qm54',
    'https://github.com/advisories/GHSA-m8rv-5g2x-5cg5',
    'https://github.com/advisories/GHSA-mh99-v99m-4gvg',
    'https://github.com/advisories/GHSA-rgw5-rvv9-x895',
    'https://github.com/advisories/GHSA-v3r7-h72x-cjcm'
)

function Add-DependencyVersions($Dependencies, $Found) {
    if ($null -eq $Dependencies) { return }
    foreach ($property in $Dependencies.PSObject.Properties) {
        $dependency = $property.Value
        if ($dependency.version) {
            if (-not $Found.ContainsKey($property.Name)) {
                $Found[$property.Name] = New-Object System.Collections.Generic.List[string]
            }
            if (-not $Found[$property.Name].Contains([string]$dependency.version)) {
                $Found[$property.Name].Add([string]$dependency.version)
            }
        }
        Add-DependencyVersions $dependency.dependencies $Found
    }
}

if (-not (Test-Path $NpmPath)) { throw "npm not found: $NpmPath" }
if (-not (Test-Path (Join-Path $DevSpaceDir 'package-lock.json'))) {
    throw "DevSpace package-lock.json not found: $DevSpaceDir"
}

$traySource = Join-Path $PSScriptRoot 'DevSpaceQuickTunnelTray.cs'
if (Test-Path $traySource) {
    $sourceText = Get-Content $traySource -Raw
    if ($sourceText -notmatch 'EnvironmentVariables\["DEVSPACE_SUBAGENTS"\]\s*=\s*"0"') {
        throw 'Security gate failed: DEVSPACE_SUBAGENTS=0 mitigation is no longer enforced. Re-audit required.'
    }
}

$lsText = (& $NpmPath ls @waishnav/devspace @earendil-works/pi-coding-agent undici brace-expansion protobufjs --all --json --prefix $DevSpaceDir 2>$null | Out-String)
if ([string]::IsNullOrWhiteSpace($lsText)) { throw 'npm ls returned no JSON.' }
$tree = $lsText | ConvertFrom-Json
$found = @{}
Add-DependencyVersions $tree.dependencies $found

foreach ($entry in $expectedVersions.GetEnumerator()) {
    if (-not $found.ContainsKey($entry.Key)) {
        throw "Audited dependency missing: $($entry.Key)"
    }
    $versions = @($found[$entry.Key])
    if ($versions.Count -ne 1 -or $versions[0] -ne $entry.Value) {
        throw "Audited dependency changed: $($entry.Key) expected $($entry.Value), found $($versions -join ', '). Re-audit required."
    }
}

$auditText = (& $NpmPath audit --omit=dev --json --prefix $DevSpaceDir 2>$null | Out-String)
if ([string]::IsNullOrWhiteSpace($auditText)) { throw 'npm audit returned no JSON.' }
$audit = $auditText | ConvertFrom-Json

$critical = [int]$audit.metadata.vulnerabilities.critical
if ($critical -gt 0) { throw "Security gate failed: $critical critical vulnerability/vulnerabilities." }

$vulnerabilityNames = @($audit.vulnerabilities.PSObject.Properties.Name)
$unknown = @($vulnerabilityNames | Where-Object { $_ -notin $knownVulnerablePackages })
if ($unknown.Count -gt 0) {
    throw "Security gate failed: new vulnerability package(s): $($unknown -join ', ')."
}

$advisoryUrls = New-Object System.Collections.Generic.List[string]
foreach ($property in $audit.vulnerabilities.PSObject.Properties) {
    foreach ($via in $property.Value.via) {
        if ($via -isnot [string] -and $via.url -and -not $advisoryUrls.Contains([string]$via.url)) {
            $advisoryUrls.Add([string]$via.url)
        }
    }
}
$unknownAdvisories = @($advisoryUrls | Where-Object { $_ -notin $knownAdvisoryUrls })
if ($unknownAdvisories.Count -gt 0) {
    throw "Security gate failed: new advisory/advisories: $($unknownAdvisories -join ', ')."
}

Write-Host 'Security audit gate: PASS_WITH_KNOWN_UPSTREAM_RISK'
Write-Host ("Known npm audit package findings: {0} moderate, {1} high, {2} critical." -f `
    $audit.metadata.vulnerabilities.moderate, `
    $audit.metadata.vulnerabilities.high, `
    $audit.metadata.vulnerabilities.critical)
Write-Host ("Known advisory IDs: {0}." -f $advisoryUrls.Count)
Write-Host 'Accepted only because the tray forces DEVSPACE_SUBAGENTS=0. See SECURITY_AUDIT.md.'
