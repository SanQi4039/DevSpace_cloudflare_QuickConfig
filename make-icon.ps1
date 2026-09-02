$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class DevSpaceIconNative {
    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr handle);
}
"@

function New-RoundedRectanglePath {
    param(
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height,
        [float]$Radius
    )

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $Radius * 2
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

$output = Join-Path $PSScriptRoot 'DevSpaceQuickTunnelTray.ico'
$size = 256
$bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
$graphics.Clear([System.Drawing.Color]::Transparent)

try {
    $outerRect = New-Object System.Drawing.RectangleF(8, 8, 240, 240)
    $outerPath = New-RoundedRectanglePath 8 8 240 240 48
    $outerBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $outerRect,
        [System.Drawing.Color]::FromArgb(255, 31, 225, 245),
        [System.Drawing.Color]::FromArgb(255, 157, 63, 255),
        35.0)
    $graphics.FillPath($outerBrush, $outerPath)

    $innerRect = New-Object System.Drawing.RectangleF(18, 18, 220, 220)
    $innerPath = New-RoundedRectanglePath 18 18 220 220 40
    $innerBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $innerRect,
        [System.Drawing.Color]::FromArgb(255, 7, 40, 102),
        [System.Drawing.Color]::FromArgb(255, 17, 22, 72),
        55.0)
    $graphics.FillPath($innerBrush, $innerPath)

    $cyan = [System.Drawing.Color]::FromArgb(255, 39, 226, 246)
    $blue = [System.Drawing.Color]::FromArgb(255, 58, 125, 250)
    $purple = [System.Drawing.Color]::FromArgb(255, 168, 79, 255)
    $white = [System.Drawing.Color]::FromArgb(245, 250, 252, 255)

    $portalPen = New-Object System.Drawing.Pen($cyan, 16)
    $portalPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $portalPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawArc($portalPen, 52, 48, 152, 150, 180, 180)
    $graphics.DrawLine($portalPen, 52, 123, 52, 180)
    $graphics.DrawLine($portalPen, 204, 123, 204, 180)

    $ringPen = New-Object System.Drawing.Pen($blue, 7)
    $ringPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $ringPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawArc($ringPen, 78, 74, 100, 100, 180, 180)
    $graphics.DrawLine($ringPen, 78, 124, 78, 164)
    $graphics.DrawLine($ringPen, 178, 124, 178, 164)

    $leftCodePen = New-Object System.Drawing.Pen($cyan, 10)
    $leftCodePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $leftCodePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($leftCodePen, 121, 112, 104, 128)
    $graphics.DrawLine($leftCodePen, 104, 128, 121, 144)

    $rightCodePen = New-Object System.Drawing.Pen($purple, 10)
    $rightCodePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $rightCodePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($rightCodePen, 135, 112, 152, 128)
    $graphics.DrawLine($rightCodePen, 152, 128, 135, 144)

    $pathPen = New-Object System.Drawing.Pen($cyan, 8)
    $pathPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pathPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawBezier($pathPen, 46, 212, 92, 192, 110, 178, 126, 162)
    $graphics.DrawBezier($pathPen, 126, 162, 145, 143, 157, 159, 164, 174)

    foreach ($node in @(
        @(46, 212, 13, $cyan),
        @(103, 184, 9, $white),
        @(164, 174, 8, $purple)
    )) {
        $brush = New-Object System.Drawing.SolidBrush($node[3])
        $r = [int]$node[2]
        $graphics.FillEllipse($brush, [int]$node[0] - $r, [int]$node[1] - $r, $r * 2, $r * 2)
        $brush.Dispose()
    }

    $shield = New-Object System.Drawing.Drawing2D.GraphicsPath
    $shield.AddPolygon([System.Drawing.Point[]]@(
        (New-Object System.Drawing.Point(184, 176)),
        (New-Object System.Drawing.Point(218, 188)),
        (New-Object System.Drawing.Point(214, 221)),
        (New-Object System.Drawing.Point(201, 235)),
        (New-Object System.Drawing.Point(188, 221))
    ))
    $shieldBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.RectangleF(184, 176, 36, 60)),
        $blue,
        $purple,
        55.0)
    $graphics.FillPath($shieldBrush, $shield)
    $shieldPen = New-Object System.Drawing.Pen($white, 3)
    $graphics.DrawPath($shieldPen, $shield)

    $lockPen = New-Object System.Drawing.Pen($white, 4)
    $graphics.DrawArc($lockPen, 194, 190, 14, 16, 180, 180)
    $lockBrush = New-Object System.Drawing.SolidBrush($white)
    $graphics.FillRectangle($lockBrush, 193, 198, 16, 14)

    $handle = $bitmap.GetHicon()
    try {
        $icon = [System.Drawing.Icon]::FromHandle($handle)
        $stream = [System.IO.File]::Open($output, [System.IO.FileMode]::Create)
        try {
            $icon.Save($stream)
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        [DevSpaceIconNative]::DestroyIcon($handle) | Out-Null
    }

    Write-Host "Generated $output"
}
finally {
    foreach ($resource in @(
        $lockBrush, $lockPen, $shieldPen, $shieldBrush, $shield,
        $pathPen, $rightCodePen, $leftCodePen, $ringPen, $portalPen,
        $innerBrush, $innerPath, $outerBrush, $outerPath, $graphics, $bitmap
    )) {
        if ($null -ne $resource) { $resource.Dispose() }
    }
}
