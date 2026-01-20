# Generates FireworksApp icons locally so the build/release pipelines can use prebuilt assets.
# Usage: run from repo root:  pwsh -ExecutionPolicy Bypass -File tools/Generate-FireworksIcons.ps1
# Output: Assets/Branding/FireworksApp.ico, Square150x150Logo.png, Square44x44Logo.png, plus supporting PNG sizes.

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Resolve-Path (Join-Path $root '..')
$assets = Join-Path $repo 'Assets/Branding'
New-Item -ItemType Directory -Path $assets -Force | Out-Null

Add-Type -AssemblyName System.Drawing

function Add-FireworksBurst {
    param(
        [Parameter(Mandatory)] $Graphics,
        [Parameter(Mandatory)] [double] $Cx,
        [Parameter(Mandatory)] [double] $Cy,
        [Parameter(Mandatory)] [double] $Radius,
        [Parameter(Mandatory)] [int] $Segments,
        [Parameter(Mandatory)] $Color,
        [Parameter(Mandatory)] [double] $SparkSize
    )

    for ($i = 0; $i -lt $Segments; $i++) {
        $angle = (360.0 / $Segments) * $i
        $rad = $angle * [System.Math]::PI / 180.0
        $x = $Cx + [System.Math]::Cos($rad) * $Radius
        $y = $Cy + [System.Math]::Sin($rad) * $Radius
        $pen = New-Object System.Drawing.Pen $Color, ($SparkSize * 0.7)
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $Graphics.DrawLine($pen, $Cx, $Cy, $x, $y)
        $pen.Dispose()

        $sparkRect = New-Object System.Drawing.RectangleF ($x - $SparkSize/2), ($y - $SparkSize/2), $SparkSize, $SparkSize
        $Graphics.FillEllipse((New-Object System.Drawing.SolidBrush $Color), $sparkRect)
    }
}

function New-FireworksBitmap {
    param(
        [Parameter(Mandatory)] [int] $Size
    )
    $bmp = New-Object System.Drawing.Bitmap $Size, $Size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    $rect = New-Object System.Drawing.Rectangle 0, 0, $Size, $Size
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush -ArgumentList @(
        $rect,
        [System.Drawing.Color]::FromArgb(255, 8, 10, 30),
        [System.Drawing.Color]::FromArgb(255, 40, 70, 160),
        [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal
    )
    $g.FillRectangle($bg, $rect)
    $bg.Dispose()

    $glowPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $glowPath.AddEllipse($Size * 0.2, $Size * 0.18, $Size * 0.6, $Size * 0.6)
    $glow = New-Object System.Drawing.Drawing2D.PathGradientBrush $glowPath
    $glow.CenterColor = [System.Drawing.Color]::FromArgb(140, 255, 230, 140)
    $glow.SurroundColors = ,([System.Drawing.Color]::FromArgb(0, 255, 230, 140))
    $g.FillPath($glow, $glowPath)
    $glow.Dispose()
    $glowPath.Dispose()

    $palette = @(
        [System.Drawing.Color]::FromArgb(255, 255, 214, 102),
        [System.Drawing.Color]::FromArgb(255, 255, 120, 90),
        [System.Drawing.Color]::FromArgb(255, 120, 220, 255),
        [System.Drawing.Color]::FromArgb(255, 180, 255, 200)
    )

    $center = $Size * 0.5
    $g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(170, 255, 255, 255))),
        $center - $Size * 0.05, $center - $Size * 0.05, $Size * 0.1, $Size * 0.1)

    Add-FireworksBurst -Graphics $g -Cx ($center - $Size * 0.12) -Cy ($center - $Size * 0.1) -Radius ($Size * 0.28) -Segments 20 -Color $palette[0] -SparkSize ($Size * 0.02)
    Add-FireworksBurst -Graphics $g -Cx $center -Cy ($center + $Size * 0.05) -Radius ($Size * 0.32) -Segments 24 -Color $palette[1] -SparkSize ($Size * 0.018)
    Add-FireworksBurst -Graphics $g -Cx ($center + $Size * 0.14) -Cy ($center - $Size * 0.12) -Radius ($Size * 0.24) -Segments 18 -Color $palette[2] -SparkSize ($Size * 0.016)

    $trailPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(130, 255, 255, 255)), ($Size * 0.01)
    $trailPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $trailPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $trailPoints = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new($center - $Size * 0.22, $Size * 0.85)
        [System.Drawing.PointF]::new($center - $Size * 0.18, $center + $Size * 0.2)
        [System.Drawing.PointF]::new($center - $Size * 0.05, $center + $Size * 0.08)
        [System.Drawing.PointF]::new($center, $center - $Size * 0.08)
    )
    $g.DrawCurve($trailPen, $trailPoints)
    $trailPen.Dispose()

    $g.Dispose()
    return $bmp
}

function Save-Png {
    param(
        [Parameter(Mandatory)] [System.Drawing.Bitmap] $Bitmap,
        [Parameter(Mandatory)] [string] $Path
    )
    $dir = Split-Path -Parent $Path
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
}

function Save-Ico {
    param(
        [Parameter(Mandatory)] [System.Drawing.Bitmap[]] $Bitmaps,
        [Parameter(Mandatory)] [string] $Path
    )
    $dir = Split-Path -Parent $Path
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

    $fs = [System.IO.File]::Open($Path, [System.IO.FileMode]::Create)
    $bw = New-Object System.IO.BinaryWriter $fs

    $bw.Write([UInt16]0)   # reserved
    $bw.Write([UInt16]1)   # type: icon
    $bw.Write([UInt16]$Bitmaps.Count)

    $offset = 6 + (16 * $Bitmaps.Count)
    $pngBytesList = @()
    foreach ($bmp in $Bitmaps) {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $bytes = $ms.ToArray()
        $pngBytesList += ,$bytes
        $width = if ($bmp.Width -eq 256) { 0 } else { [byte]$bmp.Width }
        $height = if ($bmp.Height -eq 256) { 0 } else { [byte]$bmp.Height }
        $bw.Write([byte]$width)
        $bw.Write([byte]$height)
        $bw.Write([byte]0)       # color count
        $bw.Write([byte]0)       # reserved
        $bw.Write([UInt16]0)     # planes
        $bw.Write([UInt16]32)    # bpp
        $bw.Write([UInt32]$bytes.Length)
        $bw.Write([UInt32]$offset)
        $offset += $bytes.Length
    }

    foreach ($bytes in $pngBytesList) {
        $bw.Write($bytes)
    }

    $bw.Dispose()
    $fs.Dispose()
}

$pngSizes = @(44, 64, 128, 150, 256, 1024)
$icoSizes = @(44, 64, 128, 150, 256)
$bitmaps = @()
foreach ($s in $pngSizes) {
    $bmp = New-FireworksBitmap -Size $s
    Save-Png -Bitmap $bmp -Path (Join-Path $assets "Square${s}x${s}Logo.png")
    if ($icoSizes -contains $s) {
        $bitmaps += ,$bmp
    }
    else {
        $bmp.Dispose()
    }
}

Save-Ico -Bitmaps $bitmaps -Path (Join-Path $assets 'FireworksApp.ico')

$bitmaps | ForEach-Object { $_.Dispose() }

Write-Host "Generated icons at $assets"
