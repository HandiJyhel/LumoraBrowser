param(
    [string]$OutputDir = "Lumora.WinUI\Assets",
    [string]$AssetName = "LumoraApp"
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$resolvedOutput = Join-Path (Get-Location) $OutputDir
New-Item -ItemType Directory -Force -Path $resolvedOutput | Out-Null

function New-IconBitmap {
    param([int]$Size, [bool]$Simplified = $false)

    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $scale = $Size / 1024.0
    function S([double]$value) { return [single]($value * $scale) }

    # Les traits fins mis à l'échelle linéairement deviennent une tache floue en
    # dessous d'environ 64px. Le rendu simplifié garde donc seulement le soleil
    # central et les deux arcs "internet" les plus lisibles.
    $strokeScale = if ($Simplified) { ($Size * 3.0) / 1024.0 } else { $scale }
    function Sw([double]$value) { return [single]($value * $strokeScale) }

    $rect = New-Object System.Drawing.RectangleF((S 72), (S 72), (S 880), (S 880))
    $radius = S 190
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($rect.X, $rect.Y, $radius, $radius, 180, 90)
    $path.AddArc($rect.Right - $radius, $rect.Y, $radius, $radius, 270, 90)
    $path.AddArc($rect.Right - $radius, $rect.Bottom - $radius, $radius, $radius, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $radius, $radius, $radius, 90, 90)
    $path.CloseFigure()

    $tileBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect,
        [System.Drawing.Color]::FromArgb(255, 12, 24, 34),
        [System.Drawing.Color]::FromArgb(255, 19, 55, 51),
        [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal
    )
    $graphics.FillPath($tileBrush, $path)

    if (-not $Simplified) {
        $glowPath = New-Object System.Drawing.Drawing2D.GraphicsPath
        $glowPath.AddEllipse((S 176), (S 132), (S 640), (S 640))
        $glowBrush = New-Object System.Drawing.Drawing2D.PathGradientBrush($glowPath)
        $glowBrush.CenterPoint = New-Object System.Drawing.PointF((S 494), (S 430))
        $glowBrush.CenterColor = [System.Drawing.Color]::FromArgb(95, 255, 219, 92)
        $glowBrush.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 255, 219, 92))
        $graphics.FillPath($glowBrush, $glowPath)
        $glowBrush.Dispose()
        $glowPath.Dispose()

        $coolGlowPath = New-Object System.Drawing.Drawing2D.GraphicsPath
        $coolGlowPath.AddEllipse((S 250), (S 284), (S 610), (S 560))
        $coolGlowBrush = New-Object System.Drawing.Drawing2D.PathGradientBrush($coolGlowPath)
        $coolGlowBrush.CenterPoint = New-Object System.Drawing.PointF((S 620), (S 590))
        $coolGlowBrush.CenterColor = [System.Drawing.Color]::FromArgb(72, 88, 219, 211)
        $coolGlowBrush.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 88, 219, 211))
        $graphics.FillPath($coolGlowBrush, $coolGlowPath)
        $coolGlowBrush.Dispose()
        $coolGlowPath.Dispose()
    }

    if (-not $Simplified) {
        $borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(218, 225, 249, 240), (S 12))
        $graphics.DrawPath($borderPen, $path)
        $innerPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(70, 255, 211, 87), (S 5))
        $innerRect = New-Object System.Drawing.RectangleF((S 96), (S 96), (S 832), (S 832))
        $graphics.DrawArc($innerPen, $innerRect, 202, 122)
        $innerPen.Dispose()
    }

    $globeRect = New-Object System.Drawing.RectangleF((S 236), (S 238), (S 552), (S 552))
    $shadowPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(52, 0, 0, 0), (Sw 94))
    $shadowPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $shadowPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawArc($shadowPen, $globeRect, 156, 242)
    $shadowPen.Dispose()

    $cyanPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 67, 219, 209), (Sw 68))
    $cyanPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $cyanPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $cyanPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $graphics.DrawArc($cyanPen, $globeRect, 204, 236)

    $warmPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 255, 188, 64), (Sw 48))
    $warmPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $warmPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $warmPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $graphics.DrawArc($warmPen, $globeRect, 304, 124)

    $whitePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 235, 255, 251), (Sw 40))
    $whitePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $whitePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $whitePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $graphics.DrawArc($whitePen, $globeRect, 54, 146)

    if (-not $Simplified) {
        $meridianPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(150, 235, 255, 251), (S 21))
        $meridianPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $meridianPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $graphics.DrawArc($meridianPen, (S 344), (S 236), (S 336), (S 552), 108, 232)
        $graphics.DrawArc($meridianPen, (S 236), (S 394), (S 552), (S 236), 12, 157)
        $meridianPen.Dispose()

        $routePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(212, 118, 232, 224), (S 18))
        $routePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $routePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $graphics.DrawBezier($routePen, (S 310), (S 612), (S 466), (S 700), (S 642), (S 681), (S 744), (S 552))
        $routePen.Dispose()
    }

    $sunSize = if ($Simplified) { Sw 180 } else { S 172 }
    $sunX = (S 512) - ($sunSize / 2.0)
    $sunY = (S 508) - ($sunSize / 2.0)
    $sunRect = New-Object System.Drawing.RectangleF($sunX, $sunY, $sunSize, $sunSize)
    $sunGlowMargin = S 42
    $sunGlowSize = S 84
    $sunGlowX = $sunRect.X - $sunGlowMargin
    $sunGlowY = $sunRect.Y - $sunGlowMargin
    $sunGlowWidth = $sunRect.Width + $sunGlowSize
    $sunGlowHeight = $sunRect.Height + $sunGlowSize
    $sunGlowRect = New-Object System.Drawing.RectangleF($sunGlowX, $sunGlowY, $sunGlowWidth, $sunGlowHeight)
    $sunGlowPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $sunGlowPath.AddEllipse($sunGlowRect)
    $sunGlowBrush = New-Object System.Drawing.Drawing2D.PathGradientBrush($sunGlowPath)
    $sunGlowBrush.CenterColor = [System.Drawing.Color]::FromArgb(145, 255, 206, 76)
    $sunGlowBrush.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 255, 206, 76))
    $graphics.FillPath($sunGlowBrush, $sunGlowPath)
    $sunGlowBrush.Dispose()
    $sunGlowPath.Dispose()

    $sunBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $sunRect,
        [System.Drawing.Color]::FromArgb(255, 255, 230, 110),
        [System.Drawing.Color]::FromArgb(255, 255, 132, 51),
        [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal
    )
    $graphics.FillEllipse($sunBrush, $sunRect)

    if (-not $Simplified) {
        $sunCore = New-Object System.Drawing.RectangleF((S 484), (S 454), (S 82), (S 82))
        $coreBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(205, 255, 247, 185))
        $graphics.FillEllipse($coreBrush, $sunCore)
        $coreBrush.Dispose()
    }

    if (-not $Simplified) {
        $endpointRect = New-Object System.Drawing.RectangleF((S 736), (S 536), (S 70), (S 70))
        $endpointBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 105, 232, 224))
        $graphics.FillEllipse($endpointBrush, $endpointRect)
        $endpointBrush.Dispose()
    }

    $whitePen.Dispose()
    $warmPen.Dispose()
    $cyanPen.Dispose()
    $sunBrush.Dispose()
    $tileBrush.Dispose()
    $path.Dispose()
    $graphics.Dispose()
    return $bitmap
}

function Save-Png {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [string]$Path
    )
    $tempPath = "$Path.tmp"
    if (Test-Path -LiteralPath $tempPath) {
        Remove-Item -LiteralPath $tempPath -Force
    }
    $memory = New-Object System.IO.MemoryStream
    try {
        $Bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
        [System.IO.File]::WriteAllBytes($tempPath, $memory.ToArray())
    }
    finally {
        $memory.Dispose()
    }
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Force
    }
    Move-Item -LiteralPath $tempPath -Destination $Path -Force
}

function Write-Ico {
    param(
        [string]$Path,
        [byte[][]]$Images,
        [int[]]$Sizes
    )

    $stream = [System.IO.File]::Create($Path)
    $writer = New-Object System.IO.BinaryWriter($stream)
    try {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$Images.Length)

        $offset = 6 + (16 * $Images.Length)
        for ($i = 0; $i -lt $Images.Length; $i++) {
            $sizeByte = if ($Sizes[$i] -ge 256) { 0 } else { $Sizes[$i] }
            $writer.Write([byte]$sizeByte)
            $writer.Write([byte]$sizeByte)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$Images[$i].Length)
            $writer.Write([UInt32]$offset)
            $offset += $Images[$i].Length
        }

        foreach ($image in $Images) {
            $writer.Write($image)
        }
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

$sourcePath = Join-Path $resolvedOutput "$AssetName.png"
$icoPath = Join-Path $resolvedOutput "$AssetName.ico"

$source = New-IconBitmap -Size 1024
Save-Png -Bitmap $source -Path $sourcePath
$source.Dispose()

$sizes = @(256, 128, 64, 48, 32, 24, 16)
# En dessous de 64px, le dessin détaillé devient une tache floue (barre des tâches,
# barre de titre, Alt+Tab) : rendu simplifié pour ces tailles.
$simplifiedMaxSize = 48
$pngImages = New-Object System.Collections.Generic.List[byte[]]
foreach ($size in $sizes) {
    $bitmap = New-IconBitmap -Size $size -Simplified:($size -le $simplifiedMaxSize)
    $memory = New-Object System.IO.MemoryStream
    $bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngImages.Add($memory.ToArray())
    $memory.Dispose()
    $bitmap.Dispose()
}

Write-Ico -Path $icoPath -Images $pngImages.ToArray() -Sizes $sizes

Write-Host "Generated $sourcePath"
Write-Host "Generated $icoPath"
