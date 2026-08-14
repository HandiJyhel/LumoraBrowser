[CmdletBinding()]
param(
    [string]$Version = "0.93.41.0-dev",
    [string]$CleanArtifactDir = "",
    [string]$OutputDirectory = "artifacts\installer"
)

$ErrorActionPreference = "Stop"

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

function Convert-ToSetupProjectVersion {
    param([string]$ProductVersion)

    if ($ProductVersion -match '^([0-9]+)\.([0-9]+)\.([0-9]+)\.([0-9]+)\.(.+?)(-.+)?$') {
        return "$($matches[1]).$($matches[2]).$($matches[3]).$($matches[4])$($matches[6])"
    }

    return $ProductVersion
}

function Resolve-CleanArtifact {
    param(
        [string]$RepoRoot,
        [string]$Version,
        [string]$CleanArtifactDir
    )

    if ([string]::IsNullOrWhiteSpace($CleanArtifactDir)) {
        $cleanRoot = Join-Path $RepoRoot "artifacts\clean-test"
        $latest = Get-ChildItem -LiteralPath $cleanRoot -Directory -Filter "Lumora-$Version-win-x64-clean-*" |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1

        if ($null -eq $latest) {
            throw "Aucun build propre trouvé sous $cleanRoot. Lance d'abord build-clean-test-artifact.cmd."
        }

        return $latest.FullName
    }

    if ([System.IO.Path]::IsPathRooted($CleanArtifactDir)) {
        return $CleanArtifactDir
    }

    return Join-Path $RepoRoot $CleanArtifactDir
}

function Assert-RequiredPath {
    param(
        [string]$Path,
        [string]$Message
    )

    if (-not (Test-Path $Path)) {
        throw "$Message : $Path"
    }
}

function Clear-InstallerWorkspace {
    param(
        [string]$OutputRoot,
        [string]$StagingRoot,
        [string]$SetupName,
        [string]$SummaryName,
        [string]$Version
    )

    New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

    Get-ChildItem -LiteralPath $OutputRoot -File -Filter $SetupName | Remove-Item -Force
    Get-ChildItem -LiteralPath $OutputRoot -File -Filter $SummaryName | Remove-Item -Force
    Get-ChildItem -LiteralPath $OutputRoot -File -Filter "~LumoraSetup-$Version-win-x64*" | Remove-Item -Force

    foreach ($dir in @(
        $StagingRoot,
        (Join-Path $OutputRoot "staging"),
        (Join-Path $OutputRoot "staging-netfx")
    )) {
        if (Test-Path $dir) {
            Remove-Item -LiteralPath $dir -Recurse -Force
        }
    }
}

function Write-Template {
    param(
        [string]$TemplatePath,
        [string]$DestinationPath,
        [hashtable]$Tokens
    )

    $content = Get-Content -LiteralPath $TemplatePath -Raw -Encoding UTF8
    foreach ($key in $Tokens.Keys) {
        $content = $content.Replace($key, [string]$Tokens[$key])
    }
    $content | Set-Content -LiteralPath $DestinationPath -Encoding UTF8
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$templateRoot = Join-Path $PSScriptRoot "installer"
$projectTemplate = Join-Path $templateRoot "Lumora.Setup.csproj.template"
$programTemplate = Join-Path $templateRoot "Program.cs.template"

Assert-RequiredPath -Path $projectTemplate -Message "Template projet installateur introuvable"
Assert-RequiredPath -Path $programTemplate -Message "Template code installateur introuvable"

$outputRoot = Join-Path $repoRoot $OutputDirectory
$stagingRoot = Join-Path $outputRoot "staging-dotnet"
$sourceDir = Join-Path $stagingRoot "src"
$publishDir = Join-Path $stagingRoot "publish"
$setupName = "LumoraSetup-$Version-win-x64.exe"
$summaryName = "LumoraSetup-$Version-win-x64.VERIFICATION.txt"
$setupPath = Join-Path $outputRoot $setupName
$summaryPath = Join-Path $outputRoot $summaryName

$cleanArtifactDir = Resolve-CleanArtifact -RepoRoot $repoRoot -Version $Version -CleanArtifactDir $CleanArtifactDir
$cleanArtifact = Get-Item -LiteralPath $cleanArtifactDir
$appSourceDir = Join-Path $cleanArtifact.FullName "app"
$verificationSource = Join-Path $appSourceDir "VERIFICATION.txt"
$appExe = Join-Path $appSourceDir "Lumora.WinUI.exe"
$iconSource = Join-Path $appSourceDir "Assets\LumoraApp.ico"
$logoSource = Join-Path $appSourceDir "Assets\LumoraApp.png"

Assert-RequiredPath -Path $appSourceDir -Message "Dossier app introuvable dans l'artefact propre"
Assert-RequiredPath -Path $appExe -Message "Exécutable introuvable dans l'artefact propre"
Assert-RequiredPath -Path $verificationSource -Message "VERIFICATION.txt introuvable dans l'artefact propre"
Assert-RequiredPath -Path $logoSource -Message "Logo Lumora PNG introuvable dans l'artefact propre"

$fixedRuntimeSource = Join-Path $appSourceDir "FixedRuntime"
if (-not (Test-Path $fixedRuntimeSource) -or $null -eq (Get-ChildItem -LiteralPath $fixedRuntimeSource -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 1)) {
    throw "Runtime WebView2 Fixed Version absent de l'artefact propre ($fixedRuntimeSource). " +
        "Lance scripts\prepare-webview2-fixedversion.ps1 puis reconstruis le build propre avant l'installateur : " +
        "une release sans ce dossier ne peut pas afficher de page web."
}

Clear-InstallerWorkspace `
    -OutputRoot $outputRoot `
    -StagingRoot $stagingRoot `
    -SetupName $setupName `
    -SummaryName $summaryName `
    -Version $Version

New-Item -ItemType Directory -Force -Path $sourceDir | Out-Null
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

$payloadZip = Join-Path $sourceDir "app.zip"
Compress-Archive -Path (Join-Path $appSourceDir "*") -DestinationPath $payloadZip -CompressionLevel Optimal -Force

if (Test-Path $iconSource) {
    Copy-Item -LiteralPath $iconSource -Destination (Join-Path $sourceDir "LumoraApp.ico") -Force
}
Copy-Item -LiteralPath $logoSource -Destination (Join-Path $sourceDir "LumoraApp.png") -Force

# Copie directe (pas de duplication de valeurs) des memes fichiers source que
# l'app pour l'option "Installer et activer Tor" : TorTrustedRelease porte les
# empreintes SHA256 epinglees, TorEngineProvider le telechargement/verification.
# Les deux sont deja decouples de LumoraProfilePaths/WinUI (voir leurs
# commentaires), donc compilables tels quels dans ce projet WinForms autonome.
$torSourceDir = Join-Path $repoRoot "Lumora.WinUI\Tor"
$torDestDir = Join-Path $sourceDir "Tor"
Assert-RequiredPath -Path (Join-Path $torSourceDir "TorTrustedRelease.cs") -Message "TorTrustedRelease.cs introuvable"
Assert-RequiredPath -Path (Join-Path $torSourceDir "TorEngineProvider.cs") -Message "TorEngineProvider.cs introuvable"
New-Item -ItemType Directory -Force -Path $torDestDir | Out-Null
Copy-Item -LiteralPath (Join-Path $torSourceDir "TorTrustedRelease.cs") -Destination $torDestDir -Force
Copy-Item -LiteralPath (Join-Path $torSourceDir "TorEngineProvider.cs") -Destination $torDestDir -Force

$projectPath = Join-Path $sourceDir "Lumora.Setup.csproj"
$programPath = Join-Path $sourceDir "Program.cs"

Write-Template -TemplatePath $projectTemplate -DestinationPath $projectPath -Tokens @{
    "__SETUP_PROJECT_VERSION__" = Convert-ToSetupProjectVersion -ProductVersion $Version
}

Write-Template -TemplatePath $programTemplate -DestinationPath $programPath -Tokens @{
    "__PRODUCT_VERSION__" = $Version
}

dotnet publish $projectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:DebugType=None `
    /p:DebugSymbols=false `
    --output $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "Publication de l'installateur échouée avec le code $LASTEXITCODE."
}

$publishedSetup = Join-Path $publishDir "LumoraSetup.exe"
Assert-RequiredPath -Path $publishedSetup -Message "Installateur publié introuvable"

Copy-Item -LiteralPath $publishedSetup -Destination $setupPath -Force
$setupHash = Get-Sha256Hex -FilePath $setupPath

$summary = @"
Lumora - vérification de l'installateur

Version : $Version
Installateur : $setupName
SHA256 : $setupHash
Signature Sigstore : non signée pour ce build
Signature Windows : non signée Authenticode
Installation : dossier visible et modifiable ; par défaut sans privilège administrateur sous LOCALAPPDATA
Contenu : application Lumora et modules intégrés inclus ; nom complet Lumora Browser ; logo LumoraApp.png embarqué avec rendu haute qualité ; options à cocher redessinées et visibles ; code d'installateur réorganisé en template propre ; WebView2 embarqué en mode Fixed Version (aucun téléchargement, autorisation App Container posée via icacls à l'installation) ; moteur Tor installable via une case à cocher (décochée par défaut), téléchargé depuis dist.torproject.org et vérifié par empreinte SHA256 épinglée dans le code de Lumora
Dépendances : installateur et application self-contained (runtime .NET embarqué dans chacun des deux) ; aucun prérequis système à installer au préalable, fonctionne sur un poste Windows vierge
Profil : aucun profil embarqué ; aucun dossier de profil forcé au lancement
Source build propre : $($cleanArtifact.FullName)

Windows peut afficher "Éditeur inconnu" car cet installateur n'a pas encore de certificat Authenticode payant ou reconnu.
"@
$summary | Set-Content -LiteralPath $summaryPath -Encoding UTF8

& (Join-Path $repoRoot "scripts\generate-release-checksums.ps1") `
    -ArtifactPath $setupPath `
    -OutputDirectory (Join-Path $repoRoot "artifacts\signatures") `
    -ProductName "LumoraSetup-$Version"

if ($LASTEXITCODE -ne 0) {
    throw "Génération du manifeste SHA256 de l'installateur échouée avec le code $LASTEXITCODE."
}

if (Test-Path $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}

Write-Host "Installer written:"
Write-Host $setupPath
Write-Host "Installer SHA256:"
Write-Host $setupHash
