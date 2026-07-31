[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$CabPath,

    [string]$Version = ""
)

# Prepare le runtime WebView2 "Fixed Version" embarque avec Lumora, a partir
# d'un fichier .cab telecharge MANUELLEMENT depuis la page officielle
# Microsoft (aucune URL stable/scriptable n'existe pour ce telechargement -
# verifie le 31 juillet 2026, meme les outils communautaires comme
# ProKn1fe/WebView2.Runtime demandent un telechargement manuel depuis
# https://developer.microsoft.com/en-us/microsoft-edge/webview2/#download-section).
#
# Usage :
#   1. Va sur la page ci-dessus, choisis "Fixed Version" + architecture x64,
#      telecharge le .cab (~250 Mo, nom du type
#      Microsoft.WebView2.FixedVersionRuntime.<version>.x64.cab).
#   2. Lance ce script une premiere fois avec -CabPath vers ce fichier :
#      il calcule le SHA256 et l'affiche SANS RIEN EXTRAIRE tant que ce hash
#      n'a pas ete recopie manuellement dans $PinnedSha256 ci-dessous (meme
#      logique de verification que TorTrustedRelease.cs pour le moteur Tor -
#      un binaire tiers ne s'utilise pas avant que son empreinte soit
#      epinglee dans le code, pas seulement verifiee une fois en l'air).
#   3. Une fois le hash colle et le script relance, il extrait le .cab dans
#      Lumora.WinUI\FixedRuntime\<version>\ (non versionne, voir .gitignore).
#   4. Met a jour WebView2FixedVersion dans Lumora.WinUI.csproj et
#      FixedRuntimeVersion dans WebView2Bootstrap.cs pour qu'ils pointent
#      vers la meme version que ce dossier.

$ErrorActionPreference = "Stop"

# A remplacer par le SHA256 reel du .cab une fois telecharge et verifie -
# voir l'etape 2 ci-dessus. Tant que cette valeur est le placeholder, le
# script refuse d'extraire quoi que ce soit.
$PinnedSha256 = "26c07cad95615a672cde8c1843a326e18ad25d691f004347544e5e099bff9b92"

function Get-Sha256Hex {
    param([string]$FilePath)

    $stream = [System.IO.File]::OpenRead($FilePath)
    try {
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        try {
            return ([System.BitConverter]::ToString($sha256.ComputeHash($stream)) -replace "-", "").ToLowerInvariant()
        }
        finally {
            $sha256.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

if (-not (Test-Path -LiteralPath $CabPath)) {
    throw "Fichier .cab introuvable : $CabPath"
}

$cabFile = Get-Item -LiteralPath $CabPath
$actualHash = Get-Sha256Hex -FilePath $cabFile.FullName

if ($PinnedSha256 -eq "REPLACE_WITH_PINNED_SHA256_AFTER_MANUAL_VERIFICATION") {
    Write-Host "Hash SHA256 de $($cabFile.Name) :"
    Write-Host $actualHash
    Write-Host ""
    Write-Host "Aucune extraction faite. Colle ce hash dans `$PinnedSha256` en haut de ce script," -ForegroundColor Yellow
    Write-Host "puis relance la commande, pour confirmer que ce .cab est bien celui que tu voulais." -ForegroundColor Yellow
    exit 0
}

if ($actualHash -ne $PinnedSha256) {
    throw "SHA256 du .cab ($actualHash) different du hash epingle ($PinnedSha256). Fichier different de celui deja verifie - extraction refusee."
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    if ($cabFile.BaseName -match 'FixedVersionRuntime\.([0-9]+\.[0-9]+\.[0-9]+\.[0-9]+)\.') {
        $Version = $matches[1]
    }
    else {
        throw "Impossible de deduire la version depuis le nom du fichier ($($cabFile.Name)). Precise -Version explicitement."
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$destinationRoot = Join-Path $repoRoot "Lumora.WinUI\FixedRuntime"
$destination = Join-Path $destinationRoot $Version

if (Test-Path -LiteralPath $destination) {
    Remove-Item -LiteralPath $destination -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $destination | Out-Null

Write-Host "Extraction de $($cabFile.Name) vers $destination ..."
$expandResult = & expand.exe $cabFile.FullName -F:* $destination
if ($LASTEXITCODE -ne 0) {
    throw "expand.exe a echoue avec le code $LASTEXITCODE. Sortie : $expandResult"
}

# Le .cab officiel contient un dossier racine unique du type
# "Microsoft.WebView2.FixedVersionRuntime.<version>.<arch>" - on remonte son
# contenu d'un niveau pour que WebView2Bootstrap.cs (cote app) puisse pointer
# simplement sur FixedRuntime\<version>\ sans avoir a connaitre ce nom exact.
$nestedRoot = Get-ChildItem -LiteralPath $destination -Directory -Filter "Microsoft.WebView2.FixedVersionRuntime.*" -ErrorAction SilentlyContinue
if ($nestedRoot) {
    Get-ChildItem -LiteralPath $nestedRoot.FullName -Force | Move-Item -Destination $destination -Force
    Remove-Item -LiteralPath $nestedRoot.FullName -Force
}

$runtimeExe = Get-ChildItem -LiteralPath $destination -Filter "msedgewebview2.exe" -Recurse -ErrorAction SilentlyContinue
if (-not $runtimeExe) {
    throw "msedgewebview2.exe introuvable apres extraction - le .cab ne semble pas etre un runtime Fixed Version valide."
}
if ($runtimeExe.DirectoryName -ne $destination) {
    throw "msedgewebview2.exe trouve dans un sous-dossier inattendu ($($runtimeExe.DirectoryName)) - structure du .cab differente de celle prevue, verifier manuellement avant de continuer."
}

Write-Host ""
Write-Host "Runtime WebView2 Fixed Version $Version pret dans $destination." -ForegroundColor Green
Write-Host "Verifie que WebView2FixedVersion (Lumora.WinUI.csproj) et FixedRuntimeVersion" -ForegroundColor Green
Write-Host "(WebView2Bootstrap.cs) valent bien '$Version' avant de builder." -ForegroundColor Green
