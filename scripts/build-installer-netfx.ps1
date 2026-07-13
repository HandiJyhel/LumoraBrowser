[CmdletBinding()]
param(
    [string]$Version = "0.57.8-dev",
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
$stagingRoot = Join-Path $outputRoot "staging-netfx"
$sourceDir = Join-Path $stagingRoot "src"
$buildDir = Join-Path $stagingRoot "build"
$setupName = "LumoraSetup-$Version-win-x64.exe"
$setupPath = Join-Path $outputRoot $setupName

if ([string]::IsNullOrWhiteSpace($CleanArtifactDir)) {
    $cleanRoot = Join-Path $repoRoot "artifacts\clean-test"
    $latest = Get-ChildItem -LiteralPath $cleanRoot -Directory -Filter "Lumora-$Version-win-x64-clean-*" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $latest) {
        throw "Aucun build propre trouve sous $cleanRoot."
    }

    $CleanArtifactDir = $latest.FullName
}
elseif (-not [System.IO.Path]::IsPathRooted($CleanArtifactDir)) {
    $CleanArtifactDir = Join-Path $repoRoot $CleanArtifactDir
}

$cleanArtifact = Get-Item -LiteralPath $CleanArtifactDir
$appSourceDir = Join-Path $cleanArtifact.FullName "app"
$appExe = Join-Path $appSourceDir "Lumora.WinUI.exe"
$verificationSource = Join-Path $appSourceDir "VERIFICATION.txt"
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

$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    throw "vswhere est introuvable. Installe Visual Studio avec MSBuild."
}

$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\Current\Bin\amd64\MSBuild.exe" | Select-Object -First 1
if (-not $msbuild) {
    throw "MSBuild x64 est introuvable."
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
if (Test-Path $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $sourceDir | Out-Null
New-Item -ItemType Directory -Force -Path $buildDir | Out-Null

$payloadZip = Join-Path $sourceDir "app.zip"
Compress-Archive -Path (Join-Path $appSourceDir "*") -DestinationPath $payloadZip -CompressionLevel Optimal -Force

if (Test-Path $iconSource) {
    Copy-Item -LiteralPath $iconSource -Destination (Join-Path $sourceDir "LumoraApp.ico") -Force
}

$projectPath = Join-Path $sourceDir "Lumora.Setup.NetFx.csproj"
$project = @'
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    <RootNamespace>Lumora.Setup</RootNamespace>
    <AssemblyName>LumoraSetup</AssemblyName>
    <LangVersion>latest</LangVersion>
    <PlatformTarget>x64</PlatformTarget>
    <Prefer32Bit>false</Prefer32Bit>
    <ApplicationIcon Condition="Exists('LumoraApp.ico')">LumoraApp.ico</ApplicationIcon>
    <OutputPath>__BUILD_DIR__\</OutputPath>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="Microsoft.CSharp" />
    <Reference Include="System" />
    <Reference Include="System.Core" />
    <Reference Include="System.Drawing" />
    <Reference Include="System.IO.Compression" />
    <Reference Include="System.IO.Compression.FileSystem" />
    <Reference Include="System.Windows.Forms" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include="Program.cs" />
    <EmbeddedResource Include="app.zip">
      <LogicalName>app.zip</LogicalName>
    </EmbeddedResource>
  </ItemGroup>
  <ItemGroup Condition="Exists('LumoraApp.ico')">
    <None Include="LumoraApp.ico" />
  </ItemGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
</Project>
'@
$project = $project.Replace("__BUILD_DIR__", $buildDir.Replace("\", "\\"))
$project | Set-Content -LiteralPath $projectPath -Encoding UTF8

$programPath = Join-Path $sourceDir "Program.cs"
$program = @'
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Lumora.Setup
{
    internal sealed class InstallerOptions
    {
        public string InstallRoot;
        public bool CleanInstalledProfile;
        public bool CreateDesktopShortcut;
        public bool CreateStartMenuShortcut;
    }

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
        private string _installedAppExe;

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

            Controls.Add(new Label
            {
                Text = "Installer Lumora",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                AutoSize = false,
                Location = new Point(28, 24),
                Size = new Size(460, 34)
            });

            Controls.Add(new Label
            {
                Text = "Installation locale par defaut, sans privilege administrateur. Aucun profil utilisateur n'est embarque.",
                AutoSize = false,
                Location = new Point(30, 72),
                Size = new Size(500, 42)
            });

            Controls.Add(new Label
            {
                Text = "Ce build n'est pas signe Authenticode. Le fichier VERIFICATION.txt et le SHA256 sont installes avec l'application.",
                AutoSize = false,
                Location = new Point(30, 122),
                Size = new Size(500, 42),
                ForeColor = Color.FromArgb(225, 120, 24)
            });

            Controls.Add(new Label
            {
                Text = "Dossier d'installation",
                AutoSize = false,
                Location = new Point(30, 176),
                Size = new Size(500, 24),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            });

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
            _browseButton.Click += delegate { BrowseInstallPath(defaultInstallRoot); };

            Controls.Add(_installPathTextBox);
            Controls.Add(_browseButton);
            Controls.Add(new Label
            {
                Text = "Pour Program Files, lance l'installateur en administrateur puis choisis le dossier ici.",
                AutoSize = false,
                Location = new Point(32, 236),
                Size = new Size(496, 24),
                ForeColor = Color.FromArgb(181, 174, 162)
            });

            Controls.Add(new Label
            {
                Text = "Options",
                AutoSize = false,
                Location = new Point(30, 270),
                Size = new Size(500, 24),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            });

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
            _cancelButton.Click += delegate { Close(); };

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
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choisir le dossier d'installation de Lumora";
                dialog.SelectedPath = Directory.Exists(_installPathTextBox.Text)
                    ? _installPathTextBox.Text
                    : defaultInstallRoot;

                if (dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    _installPathTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void InstallButton_Click(object sender, EventArgs e)
        {
            _installButton.Enabled = false;
            _cancelButton.Enabled = false;

            try
            {
                var installRootInput = _installPathTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(installRootInput))
                    throw new InvalidOperationException("Choisis un dossier d'installation.");

                var options = new InstallerOptions
                {
                    InstallRoot = Path.GetFullPath(installRootInput),
                    CleanInstalledProfile = _cleanProfileCheckBox.Checked,
                    CreateDesktopShortcut = _desktopShortcutCheckBox.Checked,
                    CreateStartMenuShortcut = _startMenuShortcutCheckBox.Checked
                };

                Install(options);
                SetStatus("Installation terminee.");

                if (_launchCheckBox.Checked && !string.IsNullOrWhiteSpace(_installedAppExe))
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
            var traceLauncherCmd = Path.Combine(installRoot, "Lancer avec journal de diagnostic.cmd");
            var uninstallPs1 = Path.Combine(installRoot, "uninstall.ps1");
            var uninstallCmd = Path.Combine(installRoot, "Uninstall Lumora.cmd");
            var installInfo = Path.Combine(installRoot, "INSTALLATION.txt");
            var uninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Lumora";

            SetStatus("Preparation du dossier d'installation...");
            Directory.CreateDirectory(installRoot);
            if (Directory.Exists(newAppDir))
                Directory.Delete(newAppDir, true);
            Directory.CreateDirectory(newAppDir);

            SetStatus("Extraction de Lumora...");
            ExtractPayload(newAppDir);

            SetStatus("Installation des fichiers...");
            if (Directory.Exists(appDir))
                Directory.Delete(appDir, true);
            Directory.Move(newAppDir, appDir);

            if (options.CleanInstalledProfile && Directory.Exists(profileDir))
            {
                SetStatus("Nettoyage du profil installe precedent...");
                Directory.Delete(profileDir, true);
            }

            var appExe = Path.Combine(appDir, "Lumora.WinUI.exe");
            var iconPath = Path.Combine(appDir, "Assets", "LumoraApp.ico");
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
            using (var key = Registry.CurrentUser.CreateSubKey(uninstallKey))
            {
                if (key != null)
                {
                    key.SetValue("DisplayName", "Lumora");
                    key.SetValue("DisplayVersion", "__VERSION__");
                    key.SetValue("Publisher", "H.J.");
                    key.SetValue("InstallLocation", installRoot);
                    key.SetValue("DisplayIcon", appExe);
                    key.SetValue("UninstallString", Quote(uninstallCmd));
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }
        }

        private static void ExtractPayload(string destinationRoot)
        {
            var fullRoot = Path.GetFullPath(destinationRoot);
            if (!fullRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
                fullRoot += Path.DirectorySeparatorChar;

            using (var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.zip"))
            {
                if (payload == null)
                    throw new InvalidOperationException("Payload app.zip introuvable dans l'installateur.");

                using (var archive = new ZipArchive(payload, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        var targetPath = Path.GetFullPath(Path.Combine(fullRoot, entry.FullName));
                        if (!targetPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("Archive invalide.");

                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(targetPath);
                            continue;
                        }

                        var parent = Path.GetDirectoryName(targetPath);
                        if (!string.IsNullOrEmpty(parent))
                            Directory.CreateDirectory(parent);
                        entry.ExtractToFile(targetPath, true);
                    }
                }
            }
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string arguments, string workingDirectory, string iconLocation)
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                throw new InvalidOperationException("WScript.Shell indisponible.");

            dynamic shell = Activator.CreateInstance(shellType);
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
            _statusLabel.Refresh();
        }

        private static string Quote(string value)
        {
            return "\"" + value + "\"";
        }

        private static string PsQuote(string value)
        {
            return "'" + value.Replace("'", "''") + "'";
        }
    }
}
'@
$program = $program.Replace("__VERSION__", $Version)
$program | Set-Content -LiteralPath $programPath -Encoding UTF8

& $msbuild $projectPath /t:Build /p:Configuration=Release /p:Platform=x64 /nologo
if ($LASTEXITCODE -ne 0) {
    throw "Compilation NetFx de l'installateur echouee avec le code $LASTEXITCODE."
}

$builtSetup = Join-Path $buildDir "LumoraSetup.exe"
if (-not (Test-Path $builtSetup)) {
    throw "Installateur NetFx introuvable: $builtSetup"
}

Copy-Item -LiteralPath $builtSetup -Destination $setupPath -Force

$setupHash = Get-Sha256Hex -FilePath $setupPath
$summaryPath = Join-Path $outputRoot "LumoraSetup-$Version-win-x64.VERIFICATION.txt"
$summary = @"
Lumora - verification installateur

Version: $Version
Canal: Installateur local NetFx sans NuGet
Installateur: $setupName
SHA256 installateur: $setupHash
Artifact source: $($cleanArtifact.FullName)
Signature Windows: Non signee Authenticode
Date: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss K")

Cet installateur embarque le dossier app de l'artifact propre et n'embarque aucun profil utilisateur.
Il installe Lumora dans le dossier choisi, cree les raccourcis demandes et enregistre une entree de desinstallation HKCU.
"@
$summary | Set-Content -LiteralPath $summaryPath -Encoding UTF8

Write-Host "Installer written:"
Write-Host $setupPath
Write-Host "Installer SHA256:"
Write-Host $setupHash
