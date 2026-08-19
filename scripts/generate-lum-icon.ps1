param(
    [string]$OutputDir = "Lumora.WinUI\Assets",
    [string]$AssetName = "LumoraLum"
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$resolvedOutput = Join-Path (Get-Location) $OutputDir
New-Item -ItemType Directory -Force -Path $resolvedOutput | Out-Null

# Icone du fichier de sauvegarde .lum (session "Compte et ouverture",
# 2026-08-14, chantier 3) : piste "document" retenue par l'utilisateur parmi
# 3 maquettees en Artifact - la seule qui reste lisible a 16px (taille reelle
# de l'Explorateur Windows), contrairement a une simple reprise de l'icone de
# l'application (indiscernable de l'exe a cette taille). Page repliee
# classique (coin corne, comme tout fichier de donnees Windows), fond
# clair/neutre volontairement INDEPENDANT du theme clair/sombre de l'app -
# une icone de fichier doit rester lisible sur n'importe quel fond de Bureau,
# meme convention que les icones de documents Word/Excel/etc. La goutte
# Lumora (memes coordonnees/degrade que generate-app-icon.ps1, reduite et
# recentree) est imprimee dessus pour l'identite de marque.
function New-LumIconBitmap {
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

    # ── Page (coin corne en haut a droite) ──────────────────────────────────
    $pagePath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pagePath.AddLine((P 56 20), (P 160 20))
    $pagePath.AddLine((P 160 20), (P 200 60))
    $pagePath.AddLine((P 200 60), (P 200 228))
    $bottomRightArc = New-Object System.Drawing.RectangleF((S 184), (S 220), (S 16), (S 16))
    $pagePath.AddArc($bottomRightArc, 0, 90)
    $pagePath.AddLine((P 192 236), (P 56 236))
    $bottomLeftArc = New-Object System.Drawing.RectangleF((S 48), (S 220), (S 16), (S 16))
    $pagePath.AddArc($bottomLeftArc, 90, 90)
    $pagePath.AddLine((P 48 228), (P 48 28))
    $topLeftArc = New-Object System.Drawing.RectangleF((S 48), (S 20), (S 16), (S 16))
    $pagePath.AddArc($topLeftArc, 180, 90)
    $pagePath.CloseFigure()

    $pageFill = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 250, 250, 248))
    $graphics.FillPath($pageFill, $pagePath)
    $pageBorder = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 176, 176, 172), (S 3))
    $pageBorder.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $graphics.DrawPath($pageBorder, $pagePath)

    # ── Corne repliee (triangle, coin superieur droit) ──────────────────────
    $foldPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $foldPath.AddLine((P 160 20), (P 200 60))
    $foldPath.AddLine((P 200 60), (P 168 60))
    $foldArc = New-Object System.Drawing.RectangleF((S 160), (S 44), (S 16), (S 16))
    $foldPath.AddArc($foldArc, 90, 90)
    $foldPath.CloseFigure()
    $foldFill = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 222, 222, 217))
    $graphics.FillPath($foldFill, $foldPath)
    $graphics.DrawPath($pageBorder, $foldPath)

    # ── Goutte Lumora (memes coordonnees/degrade que generate-app-icon.ps1,
    #    reduite a 55% et recentree dans la page) ────────────────────────────
    function PD([double]$x, [double]$y) {
        # Transform() equivalent a translate(74,84) scale(0.55) applique AVANT
        # la mise a l'echelle S() de cette fonction (memes coordonnees source
        # 0-256 que le comparatif SVG valide avec l'utilisateur).
        return New-Object System.Drawing.PointF((S (74 + $x * 0.55)), (S (84 + $y * 0.55)))
    }

    $dropletPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $dropletPath.AddBezier((PD 128 26), (PD 128 26), (PD 208 128), (PD 208 168))
    $dropletRect = New-Object System.Drawing.RectangleF((S (74 + 48 * 0.55)), (S (84 + 88 * 0.55)), (S (160 * 0.55)), (S (160 * 0.55)))
    $dropletPath.AddArc($dropletRect, 0, 180)
    $dropletPath.AddBezier((PD 48 168), (PD 48 128), (PD 128 26), (PD 128 26))
    $dropletPath.CloseFigure()

    $gradRect = New-Object System.Drawing.RectangleF((S (74 + 40 * 0.55)), (S (84 + 20 * 0.55)), (S (176 * 0.55)), (S (220 * 0.55)))
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

    $sparkPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $sparkPath.AddBezier((PD 128 66), (PD 134 106), (PD 148 120), (PD 190 128))
    $sparkPath.AddBezier((PD 190 128), (PD 148 136), (PD 134 150), (PD 128 190))
    $sparkPath.AddBezier((PD 128 190), (PD 122 150), (PD 108 136), (PD 66 128))
    $sparkPath.AddBezier((PD 66 128), (PD 108 120), (PD 122 106), (PD 128 66))
    $sparkPath.CloseFigure()
    $sparkBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $graphics.FillPath($sparkBrush, $sparkPath)

    $sparkBrush.Dispose()
    $sparkPath.Dispose()
    $dropletBrush.Dispose()
    $dropletPath.Dispose()
    $foldFill.Dispose()
    $foldPath.Dispose()
    $pageBorder.Dispose()
    $pageFill.Dispose()
    $pagePath.Dispose()
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

$source = New-LumIconBitmap -Size 1024
Save-Png -Bitmap $source -Path $sourcePath
$source.Dispose()

# Meme jeu de tailles que LumoraApp.ico : 16/20/24/32/40/48/64/256 - 16px est
# la taille qui compte vraiment (vue Explorateur par defaut), voir le
# commentaire d'en-tete.
$sizes = @(256, 64, 48, 40, 32, 24, 20, 16)
$pngImages = New-Object System.Collections.Generic.List[byte[]]
foreach ($size in $sizes) {
    $bitmap = New-LumIconBitmap -Size $size
    $memory = New-Object System.IO.MemoryStream
    $bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngImages.Add($memory.ToArray())
    $memory.Dispose()
    $bitmap.Dispose()
}

Write-Ico -Path $icoPath -Images $pngImages.ToArray() -Sizes $sizes

Write-Host "Generated $sourcePath"
Write-Host "Generated $icoPath"
