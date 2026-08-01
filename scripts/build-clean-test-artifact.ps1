[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$Version = "0.93.10.0-dev",
    [string]$OutputRoot = "artifacts\clean-test",
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "winui-build-common.ps1")

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

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "Lumora.WinUI\Lumora.WinUI.csproj"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$artifactName = "Lumora-$Version-win-x64-clean-$timestamp"
$artifactDir = Join-Path (Join-Path $repoRoot $OutputRoot) $artifactName
$appDir = Join-Path $artifactDir "app"
$signatureDir = Join-Path $repoRoot "artifacts\signatures"

if (-not (Test-Path $project)) {
    throw "Projet WinUI introuvable: $project"
}

$msbuild = Get-WinUiMsbuildPath
$context = New-WinUiBuildContext -RepoRoot $repoRoot -ProjectName "Lumora.WinUI" -Configuration $Configuration -Platform $Platform
$xamlOutputDir = Get-WinUiOutputDir -Context $context -Configuration $Configuration -Platform $Platform -RuntimeIdentifier "win-x64"
New-Item -ItemType Directory -Force -Path $appDir | Out-Null
New-Item -ItemType Directory -Force -Path $signatureDir | Out-Null

Set-Location $repoRoot
if (-not $NoRestore) {
    Invoke-WinUiRestore -MsbuildPath $msbuild -ProjectPath $project -Context $context -Configuration $Configuration -Platform $Platform -RuntimeIdentifier "win-x64"
}
else {
    Write-Host "Restore WinUI ignore (-NoRestore): utilisation du cache local deja restaure."
}

Invoke-WinUiTarget -MsbuildPath $msbuild -ProjectPath $project -Target "Publish" -Context $context -Configuration $Configuration -Platform $Platform -RuntimeIdentifier "win-x64" -AdditionalProperties @(
    "/p:SelfContained=true",
    "/p:PublishSelfContained=true",
    "/p:PublishSingleFile=false",
    "/p:PublishDir=$appDir\"
)

$xamlIntermediateDir = Get-WinUiIntermediateOutputDir -Context $context -Configuration $Configuration -Platform $Platform -RuntimeIdentifier "win-x64"
foreach ($xbf in @("App.xbf", "MainWindow.xbf", "LumoraAppWindow.xbf", "LumoraIncognitoWindow.xbf")) {
    $xbfPath = Join-Path $xamlOutputDir $xbf
    if (-not (Test-Path $xbfPath)) {
        $xbfPath = Join-Path $xamlIntermediateDir $xbf
    }

    if (-not (Test-Path $xbfPath)) {
        throw "Fichier XAML compile introuvable apres publish: $xbf"
    }

    Copy-Item -LiteralPath $xbfPath -Destination (Join-Path $appDir $xbf) -Force
}

$appPri = "Lumora.WinUI.pri"
$appPriPath = Join-Path $xamlOutputDir $appPri
if (-not (Test-Path $appPriPath)) {
    throw "Fichier de ressources WinUI introuvable apres publish: $appPri"
}

Copy-Item -LiteralPath $appPriPath -Destination (Join-Path $appDir $appPri) -Force

$exePath = Join-Path $appDir "Lumora.WinUI.exe"
if (-not (Test-Path $exePath)) {
    throw "Executable introuvable apres build: $exePath"
}

$exeHash = Get-Sha256Hex -FilePath $exePath
$verificationPath = Join-Path $appDir "VERIFICATION.txt"
$verification = @"
Lumora - verification du build

Version: $Version
Canal: Build propre de developpement
SHA256: $exeHash
Signature Sigstore: Non signee pour ce build
Signature Windows: Non signee Authenticode
Profil: Profil vierge isole via LUMORA_PROFILE_DIR
Date: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss K")

Ce build ne contient pas de profil utilisateur embarque.
Pour tester sans toucher a votre profil Lumora normal, lancez run-clean-profile.cmd.

Windows peut afficher "Editeur inconnu" car ce build n'a pas encore de certificat Authenticode payant/reconnu.
Cette absence ne signifie pas que le fichier est dangereux ; elle signifie que Windows ne connait pas encore l'editeur.
Verifiez l'empreinte SHA256 ci-dessus si vous voulez confirmer que l'executable n'a pas ete modifie.
"@
$verification | Set-Content -LiteralPath $verificationPath -Encoding UTF8

$runnerPath = Join-Path $artifactDir "run-clean-profile.cmd"
$runner = @"
@echo off
setlocal
set "LUMORA_PROFILE_DIR=%~dp0_clean-profile"
start "" "%~dp0app\Lumora.WinUI.exe"
"@
$runner | Set-Content -LiteralPath $runnerPath -Encoding ASCII

& (Join-Path $repoRoot "scripts\generate-release-checksums.ps1") -ArtifactPath $appDir -OutputDirectory $signatureDir -ProductName "Lumora-$Version-clean"
if ($LASTEXITCODE -ne 0) {
    throw "Generation du manifeste SHA256 echouee avec le code $LASTEXITCODE."
}

Write-Host "Clean test artifact written:"
Write-Host $artifactDir
Write-Host "Executable SHA256:"
Write-Host $exeHash
