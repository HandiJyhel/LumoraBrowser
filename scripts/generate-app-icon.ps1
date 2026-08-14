param(
    [string]$OutputDir = "Lumora.WinUI\Assets",
    [string]$AssetName = "LumoraApp"
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$resolvedOutput = Join-Path (Get-Location) $OutputDir
New-Item -ItemType Directory -Force -Path $resolvedOutput | Out-Null

# Signe Lumora "goutte + etincelle renforcee" (variante C retenue le 2026-08-12,
# suite au constat que l'ancienne etincelle fine disparaissait des 48px sur le
# Bureau reel - voir MEMORY.md). Dessine en unites logiques 0-256 (memes
# coordonnees que le comparatif SVG valide avec l'utilisateur), mis a l'echelle
# par S() pour chaque taille cible. Formes PLEINES (pas de traits fins) :
# contrairement a l'ancien signe "globe/soleil" (tourbillon de traits, cf.
# docs/APP_ICON_LEGIBILITY_FIX_0_48_2.md), un remplissage plein reste net en
# le rasterisant nativement a chaque taille, sans variante simplifiee separee.
function New-IconBitmap {
    param([int]$Size)

    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $scale = $Size / 256.0
    function S([double]$value) { return [single]($value * $scale) }
    function P([double]$x, [double]$y) { return New-Object System.Drawing.PointF((S $x), (S $y)) }

    # ── Goutte ────────────────────────────────────────────────────────────
    $dropletPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $dropletPath.AddBezier((P 128 26), (P 128 26), (P 208 128), (P 208 168))
    $dropletRect = New-Object System.Drawing.RectangleF((S 48), (S 88), (S 160), (S 160))
    $dropletPath.AddArc($dropletRect, 0, 180)
    $dropletPath.AddBezier((P 48 168), (P 48 128), (P 128 26), (P 128 26))
    $dropletPath.CloseFigure()

    $gradRect = New-Object System.Drawing.RectangleF((S 40), (S 20), (S 176), (S 220))
    $dropletBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $gradRect,
        [System.Drawing.Color]::FromArgb(255, 247, 162, 79),
        [System.Drawing.Color]::FromArgb(255, 79, 179, 166),
        35.0
    )
    $blend = New-Object System.Drawing.Drawing2D.ColorBlend(3)
    $blend.Colors = @(
        [System.Drawing.Color]::FromArgb(255, 247, 162, 79),
        [System.Drawing.Color]::FromArgb(255, 224, 138, 92),
        [System.Drawing.Color]::FromArgb(255, 79, 179, 166)
    )
    $blend.Positions = @(0.0, 0.55, 1.0)
    $dropletBrush.InterpolationColors = $blend
    $graphics.FillPath($dropletBrush, $dropletPath)

    # ── Etincelle (renforcee : ~1.8x plus grande et plus epaisse que le
    #    detail d'origine, pour rester lisible une fois reduite a 32-48px) ──
    $sparkPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $sparkPath.AddBezier((P 128 66), (P 134 106), (P 148 120), (P 190 128))
    $sparkPath.AddBezier((P 190 128), (P 148 136), (P 134 150), (P 128 190))
    $sparkPath.AddBezier((P 128 190), (P 122 150), (P 108 136), (P 66 128))
    $sparkPath.AddBezier((P 66 128), (P 108 120), (P 122 106), (P 128 66))
    $sparkPath.CloseFigure()

    $sparkBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $graphics.FillPath($sparkBrush, $sparkPath)

    $sparkBrush.Dispose()
    $sparkPath.Dispose()
    $dropletBrush.Dispose()
    $dropletPath.Dispose()
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

# Meme jeu de tailles que l'ico actuellement embarque (verifie octet par
# octet avant cette refonte) : 16/20/24/32/40/48/64/256.
$sizes = @(256, 64, 48, 40, 32, 24, 20, 16)
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
