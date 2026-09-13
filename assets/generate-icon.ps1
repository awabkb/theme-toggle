$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $root "icon.ico"

$bmp = New-Object System.Drawing.Bitmap 32, 32
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

$gold = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 200, 40))
$ray = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 170, 20), 2.4)
$g.FillEllipse($gold, 9, 9, 14, 14)
for ($i = 0; $i -lt 8; $i++) {
    $angle = $i * 45.0 * [Math]::PI / 180.0
    $x1 = 16 + [int]([Math]::Cos($angle) * 14)
    $y1 = 16 + [int]([Math]::Sin($angle) * 14)
    $x2 = 16 + [int]([Math]::Cos($angle) * 10)
    $y2 = 16 + [int]([Math]::Sin($angle) * 10)
    $g.DrawLine($ray, $x1, $y1, $x2, $y2)
}

$g.Dispose()
$gold.Dispose()
$ray.Dispose()

$icon = [System.Drawing.Icon]::FromHandle($bmp.GetHicon())
$stream = [System.IO.File]::Create($out)
$icon.Save($stream)
$stream.Close()
$bmp.Dispose()
Write-Host "Wrote $out"
