using System.Text.Json;

namespace Lumora.WinUI;

internal sealed class LumoraConfig
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // Ce fichier est TOUJOURS dans %LOCALAPPDATA%\Lumora\ — jamais dans le profil custom,
    // c'est lui qui indique où chercher le profil. EXCEPTION volontaire, distincte de
    // CustomProfilePath : LUMORA_PROFILE_DIR (2026-08-14, incident reel corrige, voir
    // MEMORY.md) redirige AUSSI ce fichier a l'interieur du dossier isole - un lancement
    // de verification/invite ne doit jamais toucher le vrai config.json partage de la
    // machine (bascule d'ActiveProfileId compris). Variable reservee au dev/verification,
    // jamais positionnee par un utilisateur reel : aucun effet sur l'usage normal.
    private static readonly string LocalAppData =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static string ConfigPath =>
        IsolatedRootOverride() is { } isolatedRoot
            ? Path.Combine(isolatedRoot, "config.json")
            : Path.Combine(LocalAppData, "Lumora", "config.json");

    // Anciens noms de produit : servent uniquement à retrouver la configuration
    // d'une installation existante après le renommage vers Lumora. Jamais consultes
    // sous LUMORA_PROFILE_DIR (rien a migrer depuis un vrai ancien produit dans un
    // sandbox de test), voir Load().
    private static readonly string LegacyNovaConfigPath = Path.Combine(LocalAppData, "NovaBrowser", "config.json");
    private static readonly string LegacyPulseConfigPath = Path.Combine(LocalAppData, "PulseBrowser", "config.json");

    private static string? IsolatedRootOverride()
    {
        var value = Environment.GetEnvironmentVariable(LumoraProfilePaths.ProfileDirectoryEnvironmentVariable);
        return string.IsNullOrWhiteSpace(value) ? null : Path.GetFullPath(value);
    }

    public string? CustomProfilePath { get; set; }
    public string ActiveProfileId { get; set; } = "default";

    // Vrai des que les slides de bienvenue (presentation du logiciel, avant
    // meme la creation du premier profil) ont ete montrees une fois. Vit ICI
    // (config globale, pas dans UiSettings qui est par-profil) car ce moment
    // se produit avant qu'aucun profil n'existe encore.
    public bool WelcomeSlidesShown { get; set; }

    public static LumoraConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<LumoraConfig>(json) ?? new LumoraConfig();
                if (config.RepairLegacyCustomProfilePath())
                    config.Save();
                return config;
            }
            if (IsolatedRootOverride() is not null)
            {
                // Sandbox de test/verification neuf : rien a migrer depuis un
                // vrai ancien produit installe sur cette machine.
                return new LumoraConfig();
            }
            if (TryLoadLegacy(LegacyNovaConfigPath, out var legacyNova))
            {
                legacyNova.RepairLegacyCustomProfilePath();
                legacyNova.Save();
                return legacyNova;
            }
            if (TryLoadLegacy(LegacyPulseConfigPath, out var legacyPulse))
            {
                legacyPulse.RepairLegacyCustomProfilePath();
                legacyPulse.Save();
                return legacyPulse;
            }
        }
        catch { }
        return new LumoraConfig();
    }

    private static bool TryLoadLegacy(string path, out LumoraConfig config)
    {
        config = new LumoraConfig();
        try
        {
            if (!File.Exists(path)) return false;
            var json = File.ReadAllText(path);
            config = JsonSerializer.Deserialize<LumoraConfig>(json) ?? new LumoraConfig();
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal bool RepairLegacyCustomProfilePath()
    {
        var repaired = RepairLegacyCustomProfilePath(CustomProfilePath);
        if (string.Equals(repaired, CustomProfilePath, StringComparison.OrdinalIgnoreCase))
            return false;

        CustomProfilePath = repaired;
        return true;
    }

    internal static string? RepairLegacyCustomProfilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        if (Directory.Exists(path))
            return path;

        foreach (var candidate in LegacyProfilePathCandidates(path))
        {
            if (LooksLikeProfileDirectory(candidate))
                return candidate;
        }

        return path;
    }

    private static IEnumerable<string> LegacyProfilePathCandidates(string path)
    {
        foreach (var oldName in new[] { "PulseBrowser", "NovaBrowser" })
        {
            foreach (var newName in new[] { "LumoraBrowser", "Lumora" })
            {
                if (path.Contains(oldName, StringComparison.OrdinalIgnoreCase))
                    yield return ReplacePathSegment(path, oldName, newName);
            }
        }
    }

    private static string ReplacePathSegment(string path, string oldName, string newName)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (var i = 0; i < parts.Length; i++)
        {
            if (string.Equals(parts[i], oldName, StringComparison.OrdinalIgnoreCase))
                parts[i] = newName;
        }

        return string.Join(Path.DirectorySeparatorChar, parts);
    }

    private static bool LooksLikeProfileDirectory(string path)
    {
        if (!Directory.Exists(path))
            return false;

        return File.Exists(Path.Combine(path, "profile.lumora"))
            || File.Exists(Path.Combine(path, "profile.pulse"))
            || File.Exists(Path.Combine(path, "profile.nova"))
            || File.Exists(Path.Combine(path, "profile.json"))
            || File.Exists(Path.Combine(path, "vault.lumora"))
            || File.Exists(Path.Combine(path, "vault.pulse"))
            || File.Exists(Path.Combine(path, "vault.nova"));
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(ConfigPath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOptions));
    }
}
