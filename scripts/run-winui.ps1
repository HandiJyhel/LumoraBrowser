[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "Lumora.WinUI\Lumora.WinUI.csproj"
. (Join-Path $PSScriptRoot "winui-build-common.ps1")

if (-not (Test-Path $project)) {
    throw "Projet WinUI introuvable: $project"
}

$msbuild = Get-WinUiMsbuildPath
$context = New-WinUiBuildContext -RepoRoot $repoRoot -ProjectName "Lumora.WinUI" -Configuration "Debug" -Platform "x64"
$outputDir = Get-WinUiOutputDir -Context $context -Configuration "Debug" -Platform "x64" -RuntimeIdentifier "win-x64"
$runRoot = Join-Path $repoRoot "artifacts\tmp\winui-run\Lumora.WinUI\x64\Debug"
Set-Location $repoRoot
Invoke-WinUiRestore -MsbuildPath $msbuild -ProjectPath $project -Context $context -Configuration "Debug" -Platform "x64" -RuntimeIdentifier "win-x64"
Invoke-WinUiTarget -MsbuildPath $msbuild -ProjectPath $project -Target "Build" -Context $context -Configuration "Debug" -Platform "x64" -RuntimeIdentifier "win-x64"

New-Item -ItemType Directory -Force -Path $runRoot | Out-Null

# Dossier de lancement STABLE (pas horodate) : les raccourcis d'application web
# crees via "Installer la page active" pointent vers l'exe de CE dossier
# (Path.Combine(AppContext.BaseDirectory, "Lumora.WinUI.exe") - voir
# MainWindow.WebApps.cs). Un dossier different a chaque lancement rendait ces
# raccourcis perimes des le correctif suivant (constate reellement le
# 2026-07-26 : barre de titre et blocage popups invisibles malgre plusieurs
# corrections, parce que le raccourci pointait vers une copie figee d'avant
# tous ces changements). On ferme d'abord toute instance encore ouverte
# lancee depuis ce meme dossier, pour ne jamais echouer sur un fichier
# verrouille au moment de la copie.
$launchDir = Join-Path $runRoot "current"
$exe = Join-Path $launchDir "Lumora.WinUI.exe"

if (Test-Path -LiteralPath $exe) {
    Get-Process -Name "Lumora.WinUI" -ErrorAction SilentlyContinue | Where-Object {
        try { $_.Path -and $_.Path.StartsWith($launchDir, [System.StringComparison]::OrdinalIgnoreCase) } catch { $false }
    } | ForEach-Object {
        try { Stop-Process -Id $_.Id -Force -ErrorAction Stop } catch { }
    }
    Start-Sleep -Milliseconds 300
}

New-Item -ItemType Directory -Force -Path $launchDir | Out-Null
Copy-Item -Path (Join-Path $outputDir "*") -Destination $launchDir -Recurse -Force

if (-not (Test-Path $exe)) {
    throw "Executable WinUI introuvable apres copie de lancement: $exe"
}

Start-Process -FilePath $exe -WorkingDirectory $launchDir
