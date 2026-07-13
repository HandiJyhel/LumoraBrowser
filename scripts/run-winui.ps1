[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "Lumora.WinUI\Lumora.WinUI.csproj"
$exe = Join-Path $repoRoot "Lumora.WinUI\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\Lumora.WinUI.exe"

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

Set-Location $repoRoot
& $msbuild $project /t:Restore /p:Configuration=Debug /p:Platform=x64
if ($LASTEXITCODE -ne 0) {
    throw "Restore WinUI echoue avec le code $LASTEXITCODE."
}

& $msbuild $project /t:Build /p:Configuration=Debug /p:Platform=x64
if ($LASTEXITCODE -ne 0) {
    throw "Build WinUI echoue avec le code $LASTEXITCODE."
}

if (-not (Test-Path $exe)) {
    throw "Executable WinUI introuvable apres build: $exe"
}

Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe)
