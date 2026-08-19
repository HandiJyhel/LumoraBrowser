using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Lumora.WinUI;

namespace Lumora.WinUI.Tor;

// Gere le cycle de vie du processus tor.exe (demarrage, suivi du bootstrap,
// arret). Ne telecharge rien : suppose le moteur deja present a
// ExpectedExecutablePath (etape suivante, hors perimetre de cette version).
// Chemins isoles au profil Lumora, jamais partages avec un Tor systeme.
// Avant tout lancement, l'integrite du binaire est verifiee par empreinte
// SHA256 contre la liste epinglee de TorTrustedRelease : un fichier absent,
// inconnu ou modifie n'est jamais execute (voir VerifyEngineIntegrityAsync).
internal sealed class TorProcessManager : IDisposable
{
    public const int DefaultSocksPort = 9050;
    public const int DefaultControlPort = 9051;

    // Delai minimal impose par Tor lui-meme entre deux SIGNAL NEWNYM : en
    // dessous, Tor ignore silencieusement la demande. Applique cote client
    // aussi pour donner un message honnete plutot qu'un bouton qui semble
    // agir sans rien faire.
    public static readonly TimeSpan NewCircuitCooldown = TimeSpan.FromSeconds(10);

    private Process? _process;
    private string? _cookieAuthPath;
    private DateTime? _lastNewCircuitUtc;

    public TorEngineState State { get; private set; } = TorEngineState.NotInstalled;
    public string StatusMessage { get; private set; } = "Moteur Tor non installe.";
    public int SocksPort { get; private set; } = DefaultSocksPort;
    public int ControlPort { get; private set; } = DefaultControlPort;

    // Expose en lecture seule pour TorExitCountrySelector (fichier separe) :
    // il a besoin d'authentifier son propre dialogue avec le control port
    // (SETCONF ExitNodes) avant de rendre la main a RequestNewCircuitAsync.
    public string? CookieAuthPath => _cookieAuthPath;

    public event Action<TorEngineState, string>? StateChanged;

    public static string ExpectedDirectory(LumoraProfilePaths profile) =>
        Path.Combine(profile.ProfileDir, "tor");

    public static string ExpectedExecutablePath(LumoraProfilePaths profile) =>
        Path.Combine(ExpectedDirectory(profile), "tor.exe");

    public static bool IsEngineInstalled(LumoraProfilePaths profile) =>
        File.Exists(ExpectedExecutablePath(profile));

    // Verifie que chaque fichier attendu sous ExpectedDirectory correspond a
    // une empreinte SHA256 connue (TorTrustedRelease.TrustedFileHashes).
    // Un fichier absent ou dont le hash ne correspond a aucune version
    // verifiee fait echouer la verification : StartAsync ne l'executera pas.
    public static Task<bool> VerifyEngineIntegrityAsync(
        LumoraProfilePaths profile, CancellationToken cancellationToken = default) =>
        VerifyEngineIntegrityAsync(profile, TorTrustedRelease.TrustedFileHashes, cancellationToken);

    // Surcharge testable : accepte la table de hashes en parametre plutot que
    // de toujours lire TorTrustedRelease, pour permettre aux tests de verifier
    // le cas d'acceptation sans embarquer le veritable tor.exe (~10 Mo).
    internal static async Task<bool> VerifyEngineIntegrityAsync(
        LumoraProfilePaths profile,
        IReadOnlyDictionary<string, string> trustedFileHashes,
        CancellationToken cancellationToken = default)
    {
        var directory = ExpectedDirectory(profile);
        foreach (var (fileName, expectedHash) in trustedFileHashes)
        {
            var path = Path.Combine(directory, fileName);
            if (!File.Exists(path))
            {
                return false;
            }

            var actualHash = await ComputeSha256Async(path, cancellationToken);
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public async Task<bool> StartAsync(
        LumoraProfilePaths profile, int socksPort = DefaultSocksPort, int controlPort = DefaultControlPort)
    {
        if (_process is { HasExited: false })
        {
            return true;
        }

        var exePath = ExpectedExecutablePath(profile);
        if (!File.Exists(exePath))
        {
            SetState(TorEngineState.NotInstalled, "Moteur Tor non installe.");
            return false;
        }

        if (!await VerifyEngineIntegrityAsync(profile))
        {
            SetState(TorEngineState.Error,
                "Le moteur Tor présent ne correspond à aucune version vérifiée. Lancement refusé par sécurité.");
            return false;
        }

        var dataDir = Path.Combine(ExpectedDirectory(profile), "data");
        Directory.CreateDirectory(dataDir);
        SocksPort = socksPort;
        ControlPort = controlPort;
        // Chemin par defaut ecrit par Tor quand CookieAuthentication est active
        // sans CookieAuthFile explicite : un fichier binaire dans DataDirectory,
        // lisible uniquement par l'utilisateur courant (permissions NTFS
        // heritees du dossier profil Lumora). Sert uniquement a authentifier
        // SIGNAL NEWNYM (voir RequestNewCircuitAsync) - jamais expose au reseau,
        // le control port n'ecoute que sur 127.0.0.1.
        _cookieAuthPath = Path.Combine(dataDir, "control_auth_cookie");

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
        startInfo.ArgumentList.Add("--ControlPort");
        startInfo.ArgumentList.Add(controlPort.ToString());
        startInfo.ArgumentList.Add("--CookieAuthentication");
        startInfo.ArgumentList.Add("1");
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
                // Le code de sortie et les dernieres lignes de tor.exe (traces via
                // HandleTorLogLine) sont le seul moyen de savoir POURQUOI le moteur
                // s'est arrete (port deja pris, permissions, etc.) - sans ca,
                // "Moteur Tor arrete." ne dit rien de plus qu'un echec generique.
                WinUiRuntimeTrace.Write($"tor.exe exited (code={SafeExitCode(process)})");
                if (State != TorEngineState.Error)
                {
                    SetState(TorEngineState.Stopped, "Moteur Tor arrete.");
                }
            };

            if (!process.Start())
            {
                WinUiRuntimeTrace.Write("tor.exe: process.Start() a retourne false");
                SetState(TorEngineState.Error, "Impossible de demarrer le moteur Tor.");
                return false;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _process = process;
            SetState(TorEngineState.Starting, "Connexion au reseau Tor en cours...");
            return true;
        }
        catch (Exception ex)
        {
            WinUiRuntimeTrace.Write($"tor.exe: exception au demarrage : {ex}");
            SetState(TorEngineState.Error, $"Moteur Tor indisponible : {ex.Message}");
            return false;
        }
    }

    private static int? SafeExitCode(Process process)
    {
        try { return process.ExitCode; } catch { return null; }
    }

    // Demande a Tor de batir de nouveaux circuits (SIGNAL NEWNYM sur le
    // control port), pour echapper a un noeud de sortie mal note aupres d'un
    // systeme anti-bot (captcha qui boucle indefiniment malgre une reponse
    // correcte - symptome connu du reseau Tor, pas propre a Lumora). Les
    // connexions deja ouvertes gardent leur circuit jusqu'a leur fermeture ;
    // l'appelant doit recharger la page pour beneficier des nouveaux circuits.
    // Ne ferme jamais la fenetre ni ne recree le process, contrairement au
    // changement de port SOCKS (fige au demarrage, voir StartAsync).
    public async Task<(bool Success, string Message)> RequestNewCircuitAsync(
        CancellationToken cancellationToken = default)
    {
        if (_process is not { HasExited: false } || State != TorEngineState.Connected)
        {
            return (false, "Le moteur Tor n'est pas connecte.");
        }

        var (allowed, remaining) = EvaluateCooldown(_lastNewCircuitUtc, DateTime.UtcNow, NewCircuitCooldown);
        if (!allowed)
        {
            return (false, $"Attends encore {Math.Ceiling(remaining.TotalSeconds)}s avant un nouveau circuit.");
        }

        if (_cookieAuthPath is null || !File.Exists(_cookieAuthPath))
        {
            return (false, "Authentification du contrôle Tor indisponible.");
        }

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, ControlPort, cancellationToken);
            await using var stream = client.GetStream();
            using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" };
            using var reader = new StreamReader(stream, Encoding.ASCII);

            var cookieBytes = await File.ReadAllBytesAsync(_cookieAuthPath, cancellationToken);
            var cookieHex = Convert.ToHexString(cookieBytes);

            await writer.WriteLineAsync($"AUTHENTICATE {cookieHex}");
            var authResponse = await reader.ReadLineAsync(cancellationToken);
            if (authResponse is null || !authResponse.StartsWith("250", StringComparison.Ordinal))
            {
                return (false, "Authentification aupres du controle Tor refusee.");
            }

            await writer.WriteLineAsync("SIGNAL NEWNYM");
            var signalResponse = await reader.ReadLineAsync(cancellationToken);
            if (signalResponse is null || !signalResponse.StartsWith("250", StringComparison.Ordinal))
            {
                return (false, "Le moteur Tor a refuse la demande de nouveau circuit.");
            }

            await writer.WriteLineAsync("QUIT");
            _lastNewCircuitUtc = DateTime.UtcNow;
            return (true, "Nouveau circuit Tor demande.");
        }
        catch (Exception ex)
        {
            return (false, $"Nouveau circuit impossible : {ex.Message}");
        }
    }

    // Logique pure (testable sans control port reel) : autorise une demande
    // de nouveau circuit si aucune demande precedente, ou si le cooldown
    // impose par Tor est ecoule.
    internal static (bool Allowed, TimeSpan Remaining) EvaluateCooldown(
        DateTime? lastRequestUtc, DateTime nowUtc, TimeSpan cooldown)
    {
        if (lastRequestUtc is not { } last)
        {
            return (true, TimeSpan.Zero);
        }

        var elapsed = nowUtc - last;
        return elapsed >= cooldown ? (true, TimeSpan.Zero) : (false, cooldown - elapsed);
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
        catch (Exception ex)
        {
            // Un echec silencieux ici laisserait potentiellement tor.exe tourner
            // en arriere-plan sans qu'aucune trace ne le signale - source exacte
            // de l'incident du 31 juillet (processus orphelin retenant les ports
            // 9050/9051, diagnostique uniquement en interrogeant les processus
            // reels de la machine faute de log).
            WinUiRuntimeTrace.Write($"TorProcessManager.Stop: echec Kill : {ex.GetType().Name}: {ex.Message}");
        }
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

        // Trace integrale (pas seulement le pourcentage de bootstrap) : avant ce
        // correctif, tout le reste de la sortie de tor.exe (erreurs de port deja
        // pris, permissions, etc.) etait silencieusement jete, rendant un echec
        // de demarrage indiagnosticable autrement que par "Moteur Tor arrete.".
        WinUiRuntimeTrace.Write($"tor.exe: {line}");

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
