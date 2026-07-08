[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$cargoBin = Join-Path $env:USERPROFILE ".cargo\bin"
$ninjaDir = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages\Ninja-build.Ninja_Microsoft.Winget.Source_8wekyb3d8bbwe"

Write-Warning "Prototype Win32/CEF legacy: reference technique uniquement. Ne pas l'utiliser pour developper l'interface produit."

if (Test-Path $cargoBin) {
    $env:Path = "$cargoBin;$env:Path"
}

if (Test-Path $ninjaDir) {
    $env:Path = "$ninjaDir;$env:Path"
}

$cargo = Get-Command cargo -ErrorAction SilentlyContinue
if (-not $cargo) {
    throw "Cargo est introuvable. Installe Rust avec rustup, puis relance ce script."
}

Set-Location $repoRoot
cargo run
