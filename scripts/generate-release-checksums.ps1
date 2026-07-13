param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactPath,

    [string]$OutputDirectory = "artifacts/signatures",

    [string]$ProductName = "Lumora"
)

$ErrorActionPreference = "Stop"

function Get-Sha256Hex {
    param([string]$FilePath)

    $stream = [System.IO.File]::OpenRead($FilePath)
    try {
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        try {
            $hash = $sha256.ComputeHash($stream)
            return ([System.BitConverter]::ToString($hash) -replace "-", "").ToLowerInvariant()
        }
        finally {
            $sha256.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Get-RelativeArtifactPath {
    param(
        [string]$BasePath,
        [string]$FilePath
    )

    $baseUri = [Uri]((Resolve-Path -LiteralPath $BasePath).Path.TrimEnd('\') + '\')
    $fileUri = [Uri](Resolve-Path -LiteralPath $FilePath).Path
    return [Uri]::UnescapeDataString($baseUri.MakeRelativeUri($fileUri).ToString())
}

$resolvedArtifact = Resolve-Path -LiteralPath $ArtifactPath
$resolvedOutput = New-Item -ItemType Directory -Force -Path $OutputDirectory

if ((Get-Item -LiteralPath $resolvedArtifact.Path).PSIsContainer) {
    $basePath = $resolvedArtifact.Path
    $files = Get-ChildItem -LiteralPath $resolvedArtifact.Path -File -Recurse |
        Where-Object {
            $_.FullName -notmatch '\\signatures\\' -and
            $_.Name -notlike '*.sha256' -and
            $_.Name -notlike '*.sigstore.json'
        } |
        Sort-Object FullName
} else {
    $basePath = Split-Path -Parent $resolvedArtifact.Path
    $files = @(Get-Item -LiteralPath $resolvedArtifact.Path)
}

if ($files.Count -eq 0) {
    throw "No artifact file found under '$ArtifactPath'."
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$manifestPath = Join-Path $resolvedOutput.FullName "$ProductName-$timestamp.sha256"

$lines = foreach ($file in $files) {
    $hash = Get-Sha256Hex -FilePath $file.FullName
    $relativePath = Get-RelativeArtifactPath -BasePath $basePath -FilePath $file.FullName
    "$hash  $relativePath"
}

$lines | Set-Content -LiteralPath $manifestPath -Encoding UTF8

Write-Host "SHA256 manifest written:"
Write-Host $manifestPath
