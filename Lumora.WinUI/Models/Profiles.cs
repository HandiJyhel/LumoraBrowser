using System.Text;

namespace Lumora.WinUI;

internal sealed record LumoraProfileEntry(
    string Id,
    string Name,
    string ProfileDir,
    bool IsActive,
    bool IsCustom)
{
    public string Label => IsCustom ? $"{Name} - emplacement personnalise" : Name;

    // Lu a chaque acces (pas mis en cache) : ces entrees sont reconstruites a
    // chaque Discover(), un cache deviendrait perime des qu'un autre profil
    // change son avatar. Cout negligeable, la liste des profils locaux reste
    // courte.
    public string? AvatarPath => ProfileAvatarResolver.Find(ProfileDir);
}

// Vue d'affichage du selecteur de profil (ProfilePickerList, MainWindow.xaml) :
// separee de LumoraProfileEntry (internal, chemins reels compris) pour ne
// donner au binding XAML que ce qui doit vraiment s'afficher.
public sealed record ProfilePickerItem(string Name, string TypeLabel, string? AvatarPath, bool IsActive);

// Resolution de l'avatar local d'un profil (avatar.<ext> dans son dossier).
// Partagee entre le profil actif (MainWindow.Avatar.cs) et les profils
// listes/pas encore charges (selecteur, gestion des utilisateurs) - vit ici
// (Models/) plutot que dans MainWindow car aucun type WinUI n'est requis et
// Models/ est aussi compile par Lumora.Tests (sans reference a
// Microsoft.UI.Xaml).
internal static class ProfileAvatarResolver
{
    public static readonly string[] Extensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp"];

    public static string? Find(string? profileDir)
    {
        if (string.IsNullOrWhiteSpace(profileDir)) return null;
        foreach (var ext in Extensions)
        {
            var path = Path.Combine(profileDir, "avatar" + ext);
            if (File.Exists(path)) return path;
        }
        return null;
    }
}

internal static class LumoraProfileRegistry
{
    public static string QuarantineProfile(LumoraProfileEntry entry)
    {
        if (entry.IsActive)
            throw new InvalidOperationException("Le profil actif ne peut pas etre mis en quarantaine.");

        if (string.IsNullOrWhiteSpace(entry.ProfileDir) || !Directory.Exists(entry.ProfileDir))
            throw new DirectoryNotFoundException("Dossier de profil introuvable.");

        var parent = Directory.GetParent(entry.ProfileDir)?.FullName
            ?? throw new InvalidOperationException("Dossier parent du profil introuvable.");
        var quarantineRoot = Path.Combine(parent, ".novabrowser-profile-quarantine");
        Directory.CreateDirectory(quarantineRoot);

        var safeName = LumoraProfilePaths.NormalizeProfileId(entry.Name);
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var target = Path.Combine(quarantineRoot, $"{safeName}-{timestamp}");
        var suffix = 2;
        while (Directory.Exists(target))
        {
            target = Path.Combine(quarantineRoot, $"{safeName}-{timestamp}-{suffix++}");
        }

        Directory.Move(entry.ProfileDir, target);
        return target;
    }

    public static List<LumoraProfileEntry> Discover(LumoraConfig config, string? activeProfileDir = null)
    {
        var entries = new List<LumoraProfileEntry>();
        activeProfileDir ??= ResolveActiveProfileDir(config);

        // Sous LUMORA_PROFILE_DIR (2026-08-14, incident reel corrige - voir
        // MEMORY.md) : un seul profil existe conceptuellement, celui du
        // dossier isole lui-meme (meme convention que Default()/ForProfileId)
        // - ne JAMAIS scanner le vrai %LOCALAPPDATA%\Lumora\profiles de la
        // machine, qui exposerait/melangerait les vrais profils de
        // l'utilisateur dans un lancement de verification/invite cense en
        // etre totalement isole.
        if (LumoraProfilePaths.HasIsolatedProfileDirOverride())
        {
            if (!string.IsNullOrWhiteSpace(activeProfileDir))
            {
                var isolatedPaths = LumoraProfilePaths.FromDirectory(activeProfileDir);
                var isolatedProfile = UserProfile.Load(isolatedPaths.ProfileFile, isolatedPaths.LegacyProfileFile);
                if (isolatedProfile is not null)
                {
                    entries.Add(new LumoraProfileEntry(
                        "default", isolatedProfile.Name, isolatedPaths.ProfileDir, true, false));
                }
            }
            return entries;
        }

        var activeId = LumoraProfilePaths.NormalizeProfileId(config.ActiveProfileId);

        if (!string.IsNullOrWhiteSpace(config.CustomProfilePath) && Directory.Exists(config.CustomProfilePath))
        {
            var customPaths = LumoraProfilePaths.FromDirectory(config.CustomProfilePath);
            var customProfile = UserProfile.Load(customPaths.ProfileFile, customPaths.LegacyProfileFile);
            if (customProfile is not null)
            {
                entries.Add(new LumoraProfileEntry(
                    "custom",
                    customProfile.Name,
                    customPaths.ProfileDir,
                    IsSameDirectory(customPaths.ProfileDir, activeProfileDir),
                    true));
            }
        }

        var root = LumoraProfilePaths.ProfilesRoot();
        if (Directory.Exists(root))
        {
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var id = LumoraProfilePaths.NormalizeProfileId(Path.GetFileName(dir));
                var paths = LumoraProfilePaths.FromDirectory(dir);
                var profile = UserProfile.Load(paths.ProfileFile, paths.LegacyProfileFile);
                if (profile is null) continue;
                entries.Add(new LumoraProfileEntry(
                    id,
                    profile.Name,
                    paths.ProfileDir,
                    IsSameDirectory(paths.ProfileDir, activeProfileDir)
                        || (string.Equals(id, activeId, StringComparison.OrdinalIgnoreCase)
                            && string.IsNullOrWhiteSpace(config.CustomProfilePath)
                            && string.IsNullOrWhiteSpace(activeProfileDir)),
                    false));
            }
        }

        return entries
            .GroupBy(entry => entry.ProfileDir, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(entry => entry.IsActive)
            .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string? ResolveActiveProfileDir(LumoraConfig config)
    {
        try
        {
            return LumoraProfilePaths.Default().ProfileDir;
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(config.CustomProfilePath))
                return config.CustomProfilePath;

            return null;
        }
    }

    private static bool IsSameDirectory(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
            return false;

        try
        {
            return string.Equals(
                Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static string CreateProfileId(string name)
    {
        var baseId = LumoraProfilePaths.NormalizeProfileId(RemoveDiacritics(name));
        if (baseId == "default")
            baseId = "profil";

        // Sous LUMORA_PROFILE_DIR, ForProfileId(id) retourne le meme dossier a
        // plat quel que soit id (voir son commentaire) : la boucle d'unicite
        // ci-dessous ferait comparer le meme chemin a l'infini. Un seul profil
        // existe conceptuellement dans un sandbox de test, aucune collision a
        // eviter - l'id de base suffit.
        if (LumoraProfilePaths.HasIsolatedProfileDirOverride())
            return baseId;

        var candidate = baseId;
        var index = 2;
        while (Directory.Exists(LumoraProfilePaths.ForProfileId(candidate).ProfileDir))
        {
            candidate = $"{baseId}-{index++}";
        }

        return candidate;
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
