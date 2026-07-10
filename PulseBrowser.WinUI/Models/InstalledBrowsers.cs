namespace PulseBrowser.WinUI;

// Dossiers "User Data" des navigateurs Chromium tiers susceptibles d'être
// installés à côté de Pulse. Partagé par l'import de favoris (BrowserImportSource)
// et l'import de mots de passe (PasswordImportSource) : mêmes profils, mêmes
// conventions de nommage de dossier.
internal static class InstalledChromiumBrowsers
{
    public static IEnumerable<(string Browser, string UserData)> Roots()
    {
        var local   = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new (string, string)[]
        {
            ("Google Chrome",  Path.Combine(local,   "Google", "Chrome", "User Data")),
            ("Microsoft Edge", Path.Combine(local,   "Microsoft", "Edge", "User Data")),
            ("Brave",          Path.Combine(local,   "BraveSoftware", "Brave-Browser", "User Data")),
            ("Chromium",       Path.Combine(local,   "Chromium", "User Data")),
            ("Vivaldi",        Path.Combine(local,   "Vivaldi", "User Data")),
            ("Opera",          Path.Combine(roaming, "Opera Software", "Opera Stable", "User Data")),
            ("Opera GX",       Path.Combine(roaming, "Opera Software", "Opera GX Stable", "User Data")),
        };
    }

    public static bool IsProfileDir(string path)
    {
        var name = Path.GetFileName(path);
        return name.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase);
    }
}

// Source détectée de mots de passe importables depuis un navigateur Chromium
// tiers installé sur la machine (Chrome, Edge, Brave, Vivaldi, Opera...).
// Le déchiffrement s'appuie sur ChromiumCredentialReader (DPAPI + AES-GCM).
public sealed record PasswordImportSource(string Browser, string Profile, string LoginDataPath, string LocalStatePath, int Count)
{
    public string Label => $"{Browser} {Profile} - {Count} identifiant(s)";

    public static List<PasswordImportSource> Discover()
    {
        var sources = new List<PasswordImportSource>();
        foreach (var (browser, userData) in InstalledChromiumBrowsers.Roots())
        {
            var localState = Path.Combine(userData, "Local State");
            if (!File.Exists(localState)) continue;

            foreach (var profileDir in Directory.EnumerateDirectories(userData).Where(InstalledChromiumBrowsers.IsProfileDir))
            {
                var loginData = Path.Combine(profileDir, "Login Data");
                if (!File.Exists(loginData)) continue;

                var count = ChromiumCredentialReader.CountLogins(loginData);
                if (count > 0)
                {
                    sources.Add(new PasswordImportSource(browser, Path.GetFileName(profileDir), loginData, localState, count));
                }
            }
        }

        return sources;
    }

    public List<(string origin, string username, string password)> ReadCredentials() =>
        ChromiumCredentialReader.ReadFrom(LocalStatePath, LoginDataPath);
}
