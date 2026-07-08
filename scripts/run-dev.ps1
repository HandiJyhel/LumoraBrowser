[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

throw "Prototype Win32/CEF desactive: utilise run-winui.cmd pour l'interface produit. Le lanceur legacy explicite se trouve dans archive/win32-cef-prototype."

$repoRoot = Split-Path -Parent $PSScriptRoot
$cargoBin = Join-Path $env:USERPROFILE ".cargo\bin"
$ninjaDir = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages\Ninja-build.Ninja_Microsoft.Winget.Source_8wekyb3d8bbwe"

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
