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
$launchDir = Join-Path $runRoot (Get-Date -Format "yyyyMMdd-HHmmss")
New-Item -ItemType Directory -Force -Path $launchDir | Out-Null
Copy-Item -Path (Join-Path $outputDir "*") -Destination $launchDir -Recurse -Force

$exe = Join-Path $launchDir "Lumora.WinUI.exe"
if (-not (Test-Path $exe)) {
    throw "Executable WinUI introuvable apres copie de lancement: $exe"
}

Start-Process -FilePath $exe -WorkingDirectory $launchDir

$obsoleteLaunchDirs = Get-ChildItem -Path $runRoot -Directory | Sort-Object LastWriteTime -Descending | Select-Object -Skip 5
foreach ($directory in $obsoleteLaunchDirs) {
    try {
        Remove-Item -LiteralPath $directory.FullName -Recurse -Force -ErrorAction Stop
    }
    catch {
        # Un ancien lancement peut encore etre ouvert ; on le laisse vivre.
    }
}
