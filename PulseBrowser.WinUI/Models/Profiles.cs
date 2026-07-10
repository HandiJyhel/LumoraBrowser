using System.Text;

namespace PulseBrowser.WinUI;

internal sealed record PulseProfileEntry(
    string Id,
    string Name,
    string ProfileDir,
    bool IsActive,
    bool IsCustom)
{
    public string Label => IsCustom ? $"{Name} - emplacement personnalise" : Name;
}

internal static class PulseProfileRegistry
{
    public static string QuarantineProfile(PulseProfileEntry entry)
    {
        if (entry.IsActive)
            throw new InvalidOperationException("Le profil actif ne peut pas etre mis en quarantaine.");

        if (string.IsNullOrWhiteSpace(entry.ProfileDir) || !Directory.Exists(entry.ProfileDir))
            throw new DirectoryNotFoundException("Dossier de profil introuvable.");

        var parent = Directory.GetParent(entry.ProfileDir)?.FullName
            ?? throw new InvalidOperationException("Dossier parent du profil introuvable.");
        var quarantineRoot = Path.Combine(parent, ".pulsebrowser-profile-quarantine");
        Directory.CreateDirectory(quarantineRoot);

        var safeName = PulseProfilePaths.NormalizeProfileId(entry.Name);
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

    public static List<PulseProfileEntry> Discover(PulseConfig config, string? activeProfileDir = null)
    {
        var entries = new List<PulseProfileEntry>();
        var activeId = PulseProfilePaths.NormalizeProfileId(config.ActiveProfileId);
        activeProfileDir ??= ResolveActiveProfileDir(config);

        if (!string.IsNullOrWhiteSpace(config.CustomProfilePath) && Directory.Exists(config.CustomProfilePath))
        {
            var customPaths = PulseProfilePaths.FromDirectory(config.CustomProfilePath);
            var customProfile = UserProfile.Load(customPaths.ProfileFile, customPaths.LegacyProfileFile);
            if (customProfile is not null)
            {
                entries.Add(new PulseProfileEntry(
                    "custom",
                    customProfile.Name,
                    customPaths.ProfileDir,
                    IsSameDirectory(customPaths.ProfileDir, activeProfileDir),
                    true));
            }
        }

        var root = PulseProfilePaths.ProfilesRoot();
        if (Directory.Exists(root))
        {
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var id = PulseProfilePaths.NormalizeProfileId(Path.GetFileName(dir));
                var paths = PulseProfilePaths.FromDirectory(dir);
                var profile = UserProfile.Load(paths.ProfileFile, paths.LegacyProfileFile);
                if (profile is null) continue;
                entries.Add(new PulseProfileEntry(
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

    private static string? ResolveActiveProfileDir(PulseConfig config)
    {
        try
        {
            return PulseProfilePaths.Default().ProfileDir;
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
        var baseId = PulseProfilePaths.NormalizeProfileId(RemoveDiacritics(name));
        if (baseId == "default")
            baseId = "profil";

        var candidate = baseId;
        var index = 2;
        while (Directory.Exists(PulseProfilePaths.ForProfileId(candidate).ProfileDir))
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
