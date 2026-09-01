$ErrorActionPreference = 'Stop'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    throw '.NET Framework 4.x C# compiler was not found.'
}

& $csc /nologo /target:winexe /optimize+ `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    /reference:System.Web.Extensions.dll `
    /reference:System.Security.dll `
    /reference:System.ServiceProcess.dll `
    /out:DevSpaceQuickTunnelTray.exe `
    DevSpaceQuickTunnelTray.cs

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host 'Built DevSpaceQuickTunnelTray.exe'
