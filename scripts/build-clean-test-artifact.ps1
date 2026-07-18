[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$Version = "0.83.23-dev",
    [string]$OutputRoot = "artifacts\clean-test",
    [switch]$NoRestore
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

$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    throw "vswhere est introuvable. Installe Visual Studio avec les outils de developpement desktop Windows."
}

$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\Current\Bin\amd64\MSBuild.exe" | Select-Object -First 1
if (-not $msbuild) {
    throw "MSBuild x64 est introuvable. Installe les outils de build Visual Studio pour WinUI 3."
}

New-Item -ItemType Directory -Force -Path $appDir | Out-Null
New-Item -ItemType Directory -Force -Path $signatureDir | Out-Null

Set-Location $repoRoot
if (-not $NoRestore) {
    & $msbuild $project /t:Restore /p:Configuration=$Configuration /p:Platform=$Platform /p:RuntimeIdentifier=win-x64
    if ($LASTEXITCODE -ne 0) {
        throw "Restore WinUI echoue avec le code $LASTEXITCODE."
    }
}
else {
    Write-Host "Restore WinUI ignore (-NoRestore): utilisation du cache local deja restaure."
}

& $msbuild $project /t:Publish /p:Configuration=$Configuration /p:Platform=$Platform /p:RuntimeIdentifier=win-x64 /p:SelfContained=true /p:PublishSelfContained=true /p:PublishSingleFile=false /p:PublishDir="$appDir\"
if ($LASTEXITCODE -ne 0) {
    throw "Publication WinUI autonome echouee avec le code $LASTEXITCODE."
}

$xamlOutputDir = Join-Path $repoRoot "Lumora.WinUI\bin\$Platform\$Configuration\net8.0-windows10.0.19041.0\win-x64"
foreach ($xbf in @("App.xbf", "MainWindow.xbf", "LumoraAppWindow.xbf", "LumoraPrivateWindow.xbf")) {
    $xbfPath = Join-Path $xamlOutputDir $xbf
    if (-not (Test-Path $xbfPath)) {
        $xbfPath = Join-Path $repoRoot "Lumora.WinUI\obj\$Platform\$Configuration\net8.0-windows10.0.19041.0\win-x64\$xbf"
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
