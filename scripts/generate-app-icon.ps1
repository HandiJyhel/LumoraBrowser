param(
    [string]$OutputDir = "PulseBrowser.WinUI\Assets"
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$resolvedOutput = Join-Path (Get-Location) $OutputDir
New-Item -ItemType Directory -Force -Path $resolvedOutput | Out-Null

function New-IconBitmap {
    param([int]$Size)

    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $scale = $Size / 1024.0
    function S([double]$value) { return [single]($value * $scale) }

    $rect = New-Object System.Drawing.RectangleF((S 72), (S 72), (S 880), (S 880))
    $radius = S 180
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($rect.X, $rect.Y, $radius, $radius, 180, 90)
    $path.AddArc($rect.Right - $radius, $rect.Y, $radius, $radius, 270, 90)
    $path.AddArc($rect.Right - $radius, $rect.Bottom - $radius, $radius, $radius, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $radius, $radius, $radius, 90, 90)
    $path.CloseFigure()

    $tileBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect,
        [System.Drawing.Color]::FromArgb(255, 248, 244, 236),
        [System.Drawing.Color]::FromArgb(255, 239, 231, 219),
        [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal
    )
    $graphics.FillPath($tileBrush, $path)

    $borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(210, 255, 255, 250), (S 12))
    $graphics.DrawPath($borderPen, $path)

    $orangePath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $orangePath.AddBezier((S 330), (S 385), (S 510), (S 195), (S 785), (S 240), (S 808), (S 470))
    $orangePath.AddBezier((S 808), (S 470), (S 830), (S 700), (S 610), (S 720), (S 478), (S 704))
    $orangePath.AddBezier((S 478), (S 704), (S 440), (S 700), (S 435), (S 745), (S 462), (S 770))

    $orangePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 225, 120, 24), (S 70))
    $orangePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $orangePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $orangePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $graphics.DrawPath($orangePen, $orangePath)

    $highlightPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(110, 255, 187, 79), (S 22))
    $highlightPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $highlightPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawBezier($highlightPen, (S 360), (S 360), (S 520), (S 220), (S 720), (S 245), (S 780), (S 340))

    $charcoalPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $charcoalPath.AddBezier((S 360), (S 398), (S 215), (S 560), (S 295), (S 735), (S 486), (S 792))
    $charcoalPath.AddBezier((S 486), (S 792), (S 620), (S 835), (S 770), (S 745), (S 835), (S 560))

    $charcoalPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 34, 33, 31), (S 38))
    $charcoalPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $charcoalPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $charcoalPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $graphics.DrawPath($charcoalPen, $charcoalPath)

    $dotRect = New-Object System.Drawing.RectangleF((S 456), (S 434), (S 118), (S 118))
    $dotBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $dotRect,
        [System.Drawing.Color]::FromArgb(255, 255, 169, 45),
        [System.Drawing.Color]::FromArgb(255, 225, 104, 15),
        [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal
    )
    $graphics.FillEllipse($dotBrush, $dotRect)

    $endpointRect = New-Object System.Drawing.RectangleF((S 760), (S 234), (S 74), (S 74))
    $endpointBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 238, 119, 18))
    $graphics.FillEllipse($endpointBrush, $endpointRect)

    $graphics.Dispose()
    return $bitmap
}

function Save-Png {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [string]$Path
    )
    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
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

$sourcePath = Join-Path $resolvedOutput "PulseBrowser.png"
$icoPath = Join-Path $resolvedOutput "PulseBrowser.ico"

$source = New-IconBitmap -Size 1024
Save-Png -Bitmap $source -Path $sourcePath
$source.Dispose()

$sizes = @(256, 128, 64, 48, 32, 24, 16)
$pngImages = New-Object System.Collections.Generic.List[byte[]]
foreach ($size in $sizes) {
    $bitmap = New-IconBitmap -Size $size
    $memory = New-Object System.IO.MemoryStream
    $bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngImages.Add($memory.ToArray())
    $memory.Dispose()
    $bitmap.Dispose()
}

Write-Ico -Path $icoPath -Images $pngImages.ToArray() -Sizes $sizes

Write-Host "Generated $sourcePath"
Write-Host "Generated $icoPath"
