function Get-WinUiMsbuildPath {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path $vswhere)) {
        throw "vswhere est introuvable. Installe Visual Studio avec les outils de developpement desktop Windows."
    }

    $msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\Current\Bin\amd64\MSBuild.exe" | Select-Object -First 1
    if (-not $msbuild) {
        throw "MSBuild x64 est introuvable. Installe les outils de build Visual Studio pour WinUI 3."
    }

    return $msbuild
}

function Reset-WinUiBuildDirectory {
    param([string]$Path)

    if (Test-Path -LiteralPath $Path) {
        Get-ChildItem -LiteralPath $Path -Force | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    }

    New-Item -ItemType Directory -Force -Path $Path | Out-Null
}

function Ensure-TrailingBackslash {
    param([string]$Path)

    if ($Path.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        return $Path
    }

    return "$Path\"
}

function New-WinUiBuildContext {
    param(
        [string]$RepoRoot,
        [string]$ProjectName = "Lumora.WinUI",
        [string]$Configuration = "Debug",
        [string]$Platform = "x64"
    )

    $projectDir = Join-Path $RepoRoot $ProjectName
    $scratchRoot = Join-Path $RepoRoot "artifacts\tmp\winui-build\$ProjectName\$Platform\$Configuration"
    $extensionsPath = Join-Path $scratchRoot "extensions"
    $intermediatePath = Join-Path $scratchRoot "obj"

    Reset-WinUiBuildDirectory -Path $extensionsPath
    Reset-WinUiBuildDirectory -Path $intermediatePath

    $sourceObj = Join-Path $projectDir "obj"
    $fallbackFiles = @(
        "project.assets.json",
        "project.nuget.cache",
        "$ProjectName.csproj.nuget.dgspec.json",
        "$ProjectName.csproj.nuget.g.props",
        "$ProjectName.csproj.nuget.g.targets"
    )

    foreach ($relativeName in $fallbackFiles) {
        $sourcePath = Join-Path $sourceObj $relativeName
        if (Test-Path -LiteralPath $sourcePath) {
            Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $extensionsPath $relativeName) -Force
        }
    }

    return [pscustomobject]@{
        ProjectName = $ProjectName
        ScratchRoot = $scratchRoot
        BaseIntermediateOutputPath = Ensure-TrailingBackslash $intermediatePath
        MSBuildProjectExtensionsPath = Ensure-TrailingBackslash $extensionsPath
        HasCachedRestore = (Test-Path -LiteralPath (Join-Path $extensionsPath "project.assets.json"))
    }
}

function Get-WinUiOutputDir {
    param(
        [psobject]$Context,
        [string]$Configuration,
        [string]$Platform,
        [string]$TargetFramework = "net8.0-windows10.0.19041.0",
        [string]$RuntimeIdentifier = "win-x64"
    )

    $outputDir = Join-Path $Context.ScratchRoot "bin\$Platform\$Configuration\$TargetFramework\$RuntimeIdentifier"
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
    return Ensure-TrailingBackslash $outputDir
}

function Get-WinUiMsbuildPropertyArgs {
    param(
        [psobject]$Context,
        [string]$Configuration,
        [string]$Platform,
        [string]$RuntimeIdentifier = "win-x64"
    )

    $outputDir = Get-WinUiOutputDir -Context $Context -Configuration $Configuration -Platform $Platform -RuntimeIdentifier $RuntimeIdentifier
    $args = @(
        "/p:Configuration=$Configuration",
        "/p:Platform=$Platform",
        "/p:BaseIntermediateOutputPath=$($Context.BaseIntermediateOutputPath)",
        "/p:MSBuildProjectExtensionsPath=$($Context.MSBuildProjectExtensionsPath)",
        "/p:OutputPath=$outputDir",
        "/p:OutDir=$outputDir"
    )

    if (-not [string]::IsNullOrWhiteSpace($RuntimeIdentifier)) {
        $args += "/p:RuntimeIdentifier=$RuntimeIdentifier"
    }

    return $args
}

function Invoke-WinUiRestore {
    param(
        [string]$MsbuildPath,
        [string]$ProjectPath,
        [psobject]$Context,
        [string]$Configuration,
        [string]$Platform,
        [string]$RuntimeIdentifier = ""
    )

    if ($Context.HasCachedRestore) {
        Write-Host "Cache NuGet WinUI detecte, restore reseau ignore pour privilegier la build locale."
        return
    }

    $args = @($ProjectPath, "/t:Restore") +
        (Get-WinUiMsbuildPropertyArgs -Context $Context -Configuration $Configuration -Platform $Platform -RuntimeIdentifier $RuntimeIdentifier) +
        @("/p:RestoreIgnoreFailedSources=true")

    & $MsbuildPath @args
    if ($LASTEXITCODE -eq 0) {
        return
    }

    if ($Context.HasCachedRestore) {
        Write-Warning "Restore WinUI indisponible, poursuite avec le cache NuGet copie dans l'espace intermediaire isole."
        return
    }

    throw "Restore WinUI echoue avec le code $LASTEXITCODE."
}

function Invoke-WinUiTarget {
    param(
        [string]$MsbuildPath,
        [string]$ProjectPath,
        [string]$Target,
        [psobject]$Context,
        [string]$Configuration,
        [string]$Platform,
        [string]$RuntimeIdentifier = "",
        [string[]]$AdditionalProperties = @()
    )

    $args = @($ProjectPath, "/t:$Target") +
        (Get-WinUiMsbuildPropertyArgs -Context $Context -Configuration $Configuration -Platform $Platform -RuntimeIdentifier $RuntimeIdentifier) +
        $AdditionalProperties

    & $MsbuildPath @args
    if ($LASTEXITCODE -ne 0) {
        throw "$Target WinUI echoue avec le code $LASTEXITCODE."
    }
}

function Get-WinUiIntermediateOutputDir {
    param(
        [psobject]$Context,
        [string]$Configuration,
        [string]$Platform,
        [string]$TargetFramework = "net8.0-windows10.0.19041.0",
        [string]$RuntimeIdentifier = "win-x64"
    )

    return Join-Path $Context.BaseIntermediateOutputPath "$Platform\$Configuration\$TargetFramework\$RuntimeIdentifier"
}
