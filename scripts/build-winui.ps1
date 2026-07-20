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
Set-Location $repoRoot
Invoke-WinUiRestore -MsbuildPath $msbuild -ProjectPath $project -Context $context -Configuration "Debug" -Platform "x64" -RuntimeIdentifier "win-x64"
Invoke-WinUiTarget -MsbuildPath $msbuild -ProjectPath $project -Target "Build" -Context $context -Configuration "Debug" -Platform "x64" -RuntimeIdentifier "win-x64"
