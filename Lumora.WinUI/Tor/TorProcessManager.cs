using System.Diagnostics;

namespace Lumora.WinUI.Tor;

// Gere le cycle de vie du processus tor.exe (demarrage, suivi du bootstrap,
// arret). Ne telecharge rien : suppose le moteur deja present a
// ExpectedExecutablePath (etape suivante, hors perimetre de cette version).
// Chemins isoles au profil Lumora, jamais partages avec un Tor systeme.
internal sealed class TorProcessManager : IDisposable
{
    public const int DefaultSocksPort = 9050;

    private Process? _process;

    public TorEngineState State { get; private set; } = TorEngineState.NotInstalled;
    public string StatusMessage { get; private set; } = "Moteur Tor non installe.";
    public int SocksPort { get; private set; } = DefaultSocksPort;

    public event Action<TorEngineState, string>? StateChanged;

    public static string ExpectedDirectory(LumoraProfilePaths profile) =>
        Path.Combine(profile.ProfileDir, "tor");

    public static string ExpectedExecutablePath(LumoraProfilePaths profile) =>
        Path.Combine(ExpectedDirectory(profile), "tor.exe");

    public static bool IsEngineInstalled(LumoraProfilePaths profile) =>
        File.Exists(ExpectedExecutablePath(profile));

    public Task<bool> StartAsync(LumoraProfilePaths profile, int socksPort = DefaultSocksPort)
    {
        if (_process is { HasExited: false })
        {
            return Task.FromResult(true);
        }

        var exePath = ExpectedExecutablePath(profile);
        if (!File.Exists(exePath))
        {
            SetState(TorEngineState.NotInstalled, "Moteur Tor non installe.");
            return Task.FromResult(false);
        }

        var dataDir = Path.Combine(ExpectedDirectory(profile), "data");
        Directory.CreateDirectory(dataDir);
        SocksPort = socksPort;

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("--SocksPort");
        startInfo.ArgumentList.Add(socksPort.ToString());
        startInfo.ArgumentList.Add("--DataDirectory");
        startInfo.ArgumentList.Add(dataDir);
        startInfo.ArgumentList.Add("--Log");
        startInfo.ArgumentList.Add("notice stdout");

        try
        {
            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) => HandleTorLogLine(e.Data);
            process.ErrorDataReceived += (_, e) => HandleTorLogLine(e.Data);
            process.Exited += (_, _) =>
            {
                if (State != TorEngineState.Error)
                {
                    SetState(TorEngineState.Stopped, "Moteur Tor arrete.");
                }
            };

            if (!process.Start())
            {
                SetState(TorEngineState.Error, "Impossible de demarrer le moteur Tor.");
                return Task.FromResult(false);
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _process = process;
            SetState(TorEngineState.Starting, "Connexion au reseau Tor en cours...");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            SetState(TorEngineState.Error, $"Moteur Tor indisponible : {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public void Stop()
    {
        if (_process is null) return;

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch { }
        finally
        {
            _process.Dispose();
            _process = null;
            SetState(TorEngineState.Stopped, "Moteur Tor arrete.");
        }
    }

    private void HandleTorLogLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        var bootstrapIndex = line.IndexOf("Bootstrapped ", StringComparison.OrdinalIgnoreCase);
        if (bootstrapIndex < 0) return;

        var percentText = line[(bootstrapIndex + "Bootstrapped ".Length)..];
        var percentEnd = percentText.IndexOf('%');
        if (percentEnd < 0) return;

        if (int.TryParse(percentText[..percentEnd], out var percent))
        {
            if (percent >= 100)
            {
                SetState(TorEngineState.Connected, "Connecte au reseau Tor.");
            }
            else
            {
                SetState(TorEngineState.Starting, $"Connexion au reseau Tor... {percent}%");
            }
        }
    }

    private void SetState(TorEngineState state, string message)
    {
        State = state;
        StatusMessage = message;
        StateChanged?.Invoke(state, message);
    }

    public void Dispose() => Stop();
}
