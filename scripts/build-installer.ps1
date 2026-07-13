[CmdletBinding()]
param(
    [string]$Version = "0.54.2-dev",
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
$outputRoot = Join-Path $repoRoot $OutputDirectory
$stagingRoot = Join-Path $outputRoot "staging-dotnet"
$sourceDir = Join-Path $stagingRoot "src"
$publishDir = Join-Path $stagingRoot "publish"
$setupName = "LumoraSetup-$Version-win-x64.exe"
$setupPath = Join-Path $outputRoot $setupName

if ([string]::IsNullOrWhiteSpace($CleanArtifactDir)) {
    $cleanRoot = Join-Path $repoRoot "artifacts\clean-test"
    $latest = Get-ChildItem -LiteralPath $cleanRoot -Directory -Filter "Lumora-$Version-win-x64-clean-*" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $latest) {
        throw "Aucun build propre trouve sous $cleanRoot. Lance d'abord build-clean-test-artifact.cmd."
    }

    $CleanArtifactDir = $latest.FullName
}
elseif (-not [System.IO.Path]::IsPathRooted($CleanArtifactDir)) {
    $CleanArtifactDir = Join-Path $repoRoot $CleanArtifactDir
}

$cleanArtifact = Get-Item -LiteralPath $CleanArtifactDir
$appSourceDir = Join-Path $cleanArtifact.FullName "app"
$verificationSource = Join-Path $appSourceDir "VERIFICATION.txt"
$appExe = Join-Path $appSourceDir "Lumora.WinUI.exe"
$iconSource = Join-Path $appSourceDir "Assets\LumoraApp.ico"

if (-not (Test-Path $appSourceDir)) {
    throw "Dossier app introuvable dans l'artifact propre: $appSourceDir"
}

if (-not (Test-Path $appExe)) {
    throw "Executable introuvable dans l'artifact propre: $appExe"
}

if (-not (Test-Path $verificationSource)) {
    throw "VERIFICATION.txt introuvable dans l'artifact propre: $verificationSource"
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

$currentSummaryName = "LumoraSetup-$Version-win-x64.VERIFICATION.txt"
Get-ChildItem -LiteralPath $outputRoot -File -Filter "LumoraSetup-*-win-x64.exe" |
    Where-Object { $_.Name -ne $setupName } |
    Remove-Item -Force
Get-ChildItem -LiteralPath $outputRoot -File -Filter "LumoraSetup-*-win-x64.VERIFICATION.txt" |
    Where-Object { $_.Name -ne $currentSummaryName } |
    Remove-Item -Force

$legacyIExpressStaging = Join-Path $outputRoot "staging"
if (Test-Path $legacyIExpressStaging) {
    Remove-Item -LiteralPath $legacyIExpressStaging -Recurse -Force
}

Get-ChildItem -LiteralPath $outputRoot -File -Filter "~LumoraSetup-$Version-win-x64*" |
    Remove-Item -Force

if (Test-Path $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $sourceDir | Out-Null
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

$payloadZip = Join-Path $sourceDir "app.zip"
Compress-Archive -Path (Join-Path $appSourceDir "*") -DestinationPath $payloadZip -CompressionLevel Optimal -Force

if (Test-Path $iconSource) {
    Copy-Item -LiteralPath $iconSource -Destination (Join-Path $sourceDir "LumoraApp.ico") -Force
}

$projectPath = Join-Path $sourceDir "Lumora.Setup.csproj"
$project = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <ApplicationIcon>LumoraApp.ico</ApplicationIcon>
    <AssemblyName>LumoraSetup</AssemblyName>
    <Version>__VERSION__</Version>
  </PropertyGroup>
  <ItemGroup>
    <EmbeddedResource Include="app.zip" LogicalName="app.zip" />
  </ItemGroup>
</Project>
'@
$project = $project.Replace("__VERSION__", $Version)
$project | Set-Content -LiteralPath $projectPath -Encoding UTF8

$programPath = Join-Path $sourceDir "Program.cs"
$program = @'
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Win32;

namespace Lumora.Setup;

internal sealed record InstallerOptions(
    string InstallRoot,
    bool CleanInstalledProfile,
    bool CreateDesktopShortcut,
    bool CreateStartMenuShortcut);

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new InstallerForm());
    }
}

internal sealed class InstallerForm : Form
{
    private readonly Label _statusLabel;
    private readonly Button _installButton;
    private readonly Button _cancelButton;
    private readonly TextBox _installPathTextBox;
    private readonly Button _browseButton;
    private readonly CheckBox _cleanProfileCheckBox;
    private readonly CheckBox _desktopShortcutCheckBox;
    private readonly CheckBox _startMenuShortcutCheckBox;
    private readonly CheckBox _launchCheckBox;
    private string? _installedAppExe;

    public InstallerForm()
    {
        Text = "Lumora Setup";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(560, 510);
        BackColor = Color.FromArgb(31, 33, 31);
        ForeColor = Color.FromArgb(255, 248, 237);
        Font = new Font("Segoe UI", 10);

        var defaultInstallRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "Lumora");

        var title = new Label
        {
            Text = "Installer Lumora",
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            AutoSize = false,
            Location = new Point(28, 24),
            Size = new Size(460, 34)
        };

        var body = new Label
        {
            Text = "Installation locale par defaut, sans privilege administrateur. Aucun profil utilisateur n'est embarque.",
            AutoSize = false,
            Location = new Point(30, 72),
            Size = new Size(500, 42)
        };

        var trust = new Label
        {
            Text = "Ce build n'est pas signe Authenticode. Le fichier VERIFICATION.txt et le SHA256 sont installes avec l'application.",
            AutoSize = false,
            Location = new Point(30, 122),
            Size = new Size(500, 42),
            ForeColor = Color.FromArgb(225, 120, 24)
        };

        var installPathTitle = new Label
        {
            Text = "Dossier d'installation",
            AutoSize = false,
            Location = new Point(30, 176),
            Size = new Size(500, 24),
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };

        _installPathTextBox = new TextBox
        {
            Text = defaultInstallRoot,
            Location = new Point(32, 204),
            Size = new Size(386, 26)
        };

        _browseButton = new Button
        {
            Text = "Parcourir...",
            Location = new Point(428, 202),
            Size = new Size(100, 30),
            FlatStyle = FlatStyle.Flat
        };
        _browseButton.Click += (_, _) => BrowseInstallPath(defaultInstallRoot);

        var installPathHelp = new Label
        {
            Text = "Pour Program Files, lance l'installateur en administrateur puis choisis le dossier ici.",
            AutoSize = false,
            Location = new Point(32, 236),
            Size = new Size(496, 24),
            ForeColor = Color.FromArgb(181, 174, 162)
        };

        var optionsTitle = new Label
        {
            Text = "Options",
            AutoSize = false,
            Location = new Point(30, 270),
            Size = new Size(500, 24),
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };

        _cleanProfileCheckBox = new CheckBox
        {
            Text = "Installation propre : supprimer le profil installe precedent",
            Checked = true,
            AutoSize = false,
            Location = new Point(32, 298),
            Size = new Size(500, 26),
            ForeColor = ForeColor
        };

        _desktopShortcutCheckBox = new CheckBox
        {
            Text = "Creer un raccourci sur le Bureau",
            Checked = true,
            AutoSize = false,
            Location = new Point(32, 328),
            Size = new Size(500, 26),
            ForeColor = ForeColor
        };

        _startMenuShortcutCheckBox = new CheckBox
        {
            Text = "Creer un raccourci dans le menu Demarrer",
            Checked = true,
            AutoSize = false,
            Location = new Point(32, 358),
            Size = new Size(500, 26),
            ForeColor = ForeColor
        };

        _launchCheckBox = new CheckBox
        {
            Text = "Lancer Lumora apres l'installation",
            Checked = true,
            AutoSize = false,
            Location = new Point(32, 388),
            Size = new Size(500, 26),
            ForeColor = ForeColor
        };

        _statusLabel = new Label
        {
            Text = "Pret a installer.",
            AutoSize = false,
            Location = new Point(30, 430),
            Size = new Size(500, 24),
            ForeColor = Color.FromArgb(181, 174, 162)
        };

        _installButton = new Button
        {
            Text = "Installer",
            Location = new Point(332, 464),
            Size = new Size(92, 32),
            BackColor = Color.FromArgb(225, 120, 24),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _installButton.FlatAppearance.BorderSize = 0;
        _installButton.Click += InstallButton_Click;

        _cancelButton = new Button
        {
            Text = "Annuler",
            Location = new Point(436, 464),
            Size = new Size(92, 32),
            FlatStyle = FlatStyle.Flat
        };
        _cancelButton.Click += (_, _) => Close();

        Controls.Add(title);
        Controls.Add(body);
        Controls.Add(trust);
        Controls.Add(installPathTitle);
        Controls.Add(_installPathTextBox);
        Controls.Add(_browseButton);
        Controls.Add(installPathHelp);
        Controls.Add(optionsTitle);
        Controls.Add(_cleanProfileCheckBox);
        Controls.Add(_desktopShortcutCheckBox);
        Controls.Add(_startMenuShortcutCheckBox);
        Controls.Add(_launchCheckBox);
        Controls.Add(_statusLabel);
        Controls.Add(_installButton);
        Controls.Add(_cancelButton);
    }

    private void BrowseInstallPath(string defaultInstallRoot)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choisir le dossier d'installation de Lumora",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_installPathTextBox.Text)
                ? _installPathTextBox.Text
                : defaultInstallRoot
        };

        if (dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            _installPathTextBox.Text = dialog.SelectedPath;
        }
    }

    private async void InstallButton_Click(object? sender, EventArgs e)
    {
        _installButton.Enabled = false;
        _cancelButton.Enabled = false;

        try
        {
            var installRootInput = _installPathTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(installRootInput))
                throw new InvalidOperationException("Choisis un dossier d'installation.");
            var installRoot = Path.GetFullPath(installRootInput);

            var options = new InstallerOptions(
                InstallRoot: installRoot,
                CleanInstalledProfile: _cleanProfileCheckBox.Checked,
                CreateDesktopShortcut: _desktopShortcutCheckBox.Checked,
                CreateStartMenuShortcut: _startMenuShortcutCheckBox.Checked);

            await Task.Run(() => Install(options));
            SetStatus("Installation terminee.");

            if (_launchCheckBox.Checked && _installedAppExe is not null)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _installedAppExe,
                    WorkingDirectory = Path.GetDirectoryName(_installedAppExe),
                    UseShellExecute = true
                });
            }

            MessageBox.Show(
                this,
                "Lumora est installe.\n\nAucun profil n'a ete copie. Lumora utilisera le profil choisi dans l'application.",
                "Lumora",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            _installButton.Enabled = true;
            _cancelButton.Enabled = true;
            SetStatus("Installation echouee.");
            MessageBox.Show(this, ex.Message, "Lumora Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Install(InstallerOptions options)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var installRoot = Path.GetFullPath(options.InstallRoot);
        var appDir = Path.Combine(installRoot, "app");
        var newAppDir = Path.Combine(installRoot, "_new_app");
        var profileDir = Path.Combine(localAppData, "Lumora", "installed-profile");
        var startMenuDir = Path.Combine(appData, "Microsoft", "Windows", "Start Menu", "Programs", "Lumora");
        var startMenuShortcut = Path.Combine(startMenuDir, "Lumora.lnk");
        var desktopShortcut = Path.Combine(desktopDir, "Lumora.lnk");
        var launcherVbs = Path.Combine(installRoot, "LumoraLauncher.vbs");
        var traceLauncherCmd = Path.Combine(installRoot, "Lancer avec journal de diagnostic.cmd");
        var uninstallPs1 = Path.Combine(installRoot, "uninstall.ps1");
        var uninstallCmd = Path.Combine(installRoot, "Uninstall Lumora.cmd");
        var installInfo = Path.Combine(installRoot, "INSTALLATION.txt");
        var uninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Lumora";

        SetStatus("Preparation du dossier d'installation...");
        Directory.CreateDirectory(installRoot);
        if (Directory.Exists(newAppDir))
            Directory.Delete(newAppDir, recursive: true);
        Directory.CreateDirectory(newAppDir);

        SetStatus("Extraction de Lumora...");
        using (var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.zip")
               ?? throw new InvalidOperationException("Payload app.zip introuvable dans l'installateur."))
        using (var archive = new ZipArchive(payload, ZipArchiveMode.Read))
        {
            archive.ExtractToDirectory(newAppDir, overwriteFiles: true);
        }

        SetStatus("Installation des fichiers...");
        if (Directory.Exists(appDir))
            Directory.Delete(appDir, recursive: true);
        Directory.Move(newAppDir, appDir);

        if (options.CleanInstalledProfile && Directory.Exists(profileDir))
        {
            SetStatus("Nettoyage du profil installe precedent...");
            Directory.Delete(profileDir, recursive: true);
        }

        var appExe = Path.Combine(appDir, "Lumora.WinUI.exe");
        var iconPath = Path.Combine(appDir, "Assets", "LumoraApp.ico");
        SetStatus("Nettoyage de l'ancien lanceur isole...");
        if (File.Exists(launcherVbs))
            File.Delete(launcherVbs);
        _installedAppExe = appExe;

        SetStatus("Creation du lanceur diagnostic...");
        File.WriteAllLines(traceLauncherCmd, new[]
        {
            "@echo off",
            "setlocal",
            "set \"LUMORA_TRACE_STARTUP=1\"",
            "start \"\" \"%~dp0app\\Lumora.WinUI.exe\""
        });

        SetStatus("Creation de la desinstallation...");
        File.WriteAllLines(uninstallPs1, new[]
        {
            "param([switch]$RemoveProfile)",
            "$ErrorActionPreference = \"Stop\"",
            "$installRoot = " + PsQuote(installRoot),
            "$profileDir = " + PsQuote(profileDir),
            "$startMenuShortcut = " + PsQuote(startMenuShortcut),
            "$desktopShortcut = " + PsQuote(desktopShortcut),
            "$uninstallKey = \"HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Lumora\"",
            "Remove-Item -LiteralPath $startMenuShortcut -Force -ErrorAction SilentlyContinue",
            "Remove-Item -LiteralPath $desktopShortcut -Force -ErrorAction SilentlyContinue",
            "Remove-Item -LiteralPath $uninstallKey -Recurse -Force -ErrorAction SilentlyContinue",
            "if ($RemoveProfile -and (Test-Path $profileDir)) { Remove-Item -LiteralPath $profileDir -Recurse -Force }",
            "if (Test-Path $installRoot) { Remove-Item -LiteralPath $installRoot -Recurse -Force }"
        });
        File.WriteAllLines(uninstallCmd, new[]
        {
            "@echo off",
            "powershell -NoProfile -ExecutionPolicy Bypass -File \"%~dp0uninstall.ps1\""
        });

        SetStatus("Creation des raccourcis...");
        if (options.CreateStartMenuShortcut)
        {
            Directory.CreateDirectory(startMenuDir);
            CreateShortcut(startMenuShortcut, appExe, string.Empty, appDir, File.Exists(iconPath) ? iconPath : appExe);
        }
        else if (File.Exists(startMenuShortcut))
        {
            File.Delete(startMenuShortcut);
        }

        if (options.CreateDesktopShortcut)
        {
            CreateShortcut(desktopShortcut, appExe, string.Empty, appDir, File.Exists(iconPath) ? iconPath : appExe);
        }
        else if (File.Exists(desktopShortcut))
        {
            File.Delete(desktopShortcut);
        }

        File.WriteAllText(installInfo,
            "Lumora installe localement" + Environment.NewLine + Environment.NewLine +
            "Emplacement: " + installRoot + Environment.NewLine +
            "Executable: " + appExe + Environment.NewLine +
            "Profil: aucun profil impose au lancement ; le choix se fait dans Lumora." + Environment.NewLine +
            "Verification: " + Path.Combine(appDir, "VERIFICATION.txt") + Environment.NewLine + Environment.NewLine +
            "Ce programme d'installation ne copie aucun profil utilisateur." + Environment.NewLine +
            "Les raccourcis lancent directement l'application, sans variable de profil forcee." + Environment.NewLine);

        SetStatus("Enregistrement dans Windows...");
        using var key = Registry.CurrentUser.CreateSubKey(uninstallKey);
        key?.SetValue("DisplayName", "Lumora");
        key?.SetValue("DisplayVersion", "__VERSION__");
        key?.SetValue("Publisher", "H.J.");
        key?.SetValue("InstallLocation", installRoot);
        key?.SetValue("DisplayIcon", appExe);
        key?.SetValue("UninstallString", Quote(uninstallCmd));
        key?.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key?.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string arguments, string workingDirectory, string iconLocation)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell indisponible.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.Arguments = arguments;
        shortcut.WorkingDirectory = workingDirectory;
        shortcut.IconLocation = iconLocation;
        shortcut.Description = "Lumora";
        shortcut.Save();
    }

    private void SetStatus(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(SetStatus), text);
            return;
        }

        _statusLabel.Text = text;
    }

    private static string Quote(string value) => "\"" + value + "\"";

    private static string PsQuote(string value) => "'" + value.Replace("'", "''") + "'";
}
'@
$program = $program.Replace("__VERSION__", $Version)
$program | Set-Content -LiteralPath $programPath -Encoding UTF8

dotnet publish $projectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    /p:PublishSingleFile=true `
    /p:DebugType=None `
    /p:DebugSymbols=false `
    --output $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "Publication de l'installateur echouee avec le code $LASTEXITCODE."
}

$publishedSetup = Join-Path $publishDir "LumoraSetup.exe"
if (-not (Test-Path $publishedSetup)) {
    throw "Installateur publie introuvable: $publishedSetup"
}

Copy-Item -LiteralPath $publishedSetup -Destination $setupPath -Force

$setupHash = Get-Sha256Hex -FilePath $setupPath
$summaryPath = Join-Path $outputRoot "LumoraSetup-$Version-win-x64.VERIFICATION.txt"
$summary = @"
Lumora - verification installateur

Version: $Version
Installateur: $setupName
SHA256: $setupHash
Signature Sigstore: Non signee pour ce build
Signature Windows: Non signee Authenticode
Installation: dossier visible et modifiable ; par defaut sans privilege administrateur sous LOCALAPPDATA
Profil: Aucun profil embarque ; aucun dossier de profil force au lancement
Source build propre: $($cleanArtifact.FullName)

Windows peut afficher "Editeur inconnu" car cet installateur n'a pas encore de certificat Authenticode payant/reconnu.
"@
$summary | Set-Content -LiteralPath $summaryPath -Encoding UTF8

& (Join-Path $repoRoot "scripts\generate-release-checksums.ps1") -ArtifactPath $setupPath -OutputDirectory (Join-Path $repoRoot "artifacts\signatures") -ProductName "LumoraSetup-$Version"
if ($LASTEXITCODE -ne 0) {
    throw "Generation du manifeste SHA256 installateur echouee avec le code $LASTEXITCODE."
}

if (Test-Path $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}

Write-Host "Installer written:"
Write-Host $setupPath
Write-Host "Installer SHA256:"
Write-Host $setupHash
