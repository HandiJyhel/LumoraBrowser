using System.Text.RegularExpressions;

namespace PulseBrowser.WinUI;

internal sealed class PulseProfilePaths
{
    private PulseProfilePaths(string profileDir)
    {
        ProfileDir          = profileDir;
        NavigationDir       = Path.Combine(profileDir, "navigation");
        BookmarksFile       = Path.Combine(NavigationDir, "bookmarks.pulse");
        LegacyBookmarksFile = Path.Combine(NavigationDir, "bookmarks.tsv");
        LegacyFavoritesFile = Path.Combine(NavigationDir, "favorites.tsv");
        FaviconsDir         = Path.Combine(NavigationDir, "favicons");
        UiSettingsFile      = Path.Combine(NavigationDir, "ui-settings.pulse");
        LegacyUiSettingsFile = Path.Combine(NavigationDir, "ui-settings.json");
        TabsFile            = Path.Combine(NavigationDir, "tabs.pulse");
        LegacyTabsFile      = Path.Combine(NavigationDir, "tabs.json");
        HistoryFile         = Path.Combine(NavigationDir, "history.pulse");
        LegacyHistoryFile   = Path.Combine(NavigationDir, "history.json");
        PasskeysFile        = Path.Combine(NavigationDir, "passkeys.pulse");
        VaultFile           = Path.Combine(profileDir, "vault.pulse");
        ProfileFile         = Path.Combine(profileDir, "profile.pulse");
        LegacyProfileFile   = Path.Combine(profileDir, "profile.json");
        BrowserDataDir      = Path.Combine(profileDir, "webview2");
    }

    public string ProfileDir           { get; }
    public string NavigationDir        { get; }
    public string BookmarksFile        { get; }
    public string LegacyBookmarksFile  { get; }
    public string LegacyFavoritesFile  { get; }
    public string FaviconsDir          { get; }
    public string UiSettingsFile       { get; }
    public string LegacyUiSettingsFile { get; }
    public string TabsFile             { get; }
    public string LegacyTabsFile       { get; }
    public string HistoryFile          { get; }
    public string LegacyHistoryFile    { get; }
    public string PasskeysFile         { get; }
    public string VaultFile            { get; }
    public string ProfileFile          { get; }
    public string LegacyProfileFile    { get; }
    public string BrowserDataDir       { get; }

    public static string ProfilesRoot()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
            localAppData = Path.GetTempPath();

        return Path.Combine(localAppData, "PulseBrowser", "profiles");
    }

    public static PulseProfilePaths Default()
    {
        var config = PulseConfig.Load();
        if (!string.IsNullOrWhiteSpace(config.CustomProfilePath)
            && Directory.Exists(config.CustomProfilePath))
        {
            return new PulseProfilePaths(config.CustomProfilePath);
        }

        return ForProfileId(config.ActiveProfileId);
    }

    public static PulseProfilePaths ForProfileId(string? profileId)
    {
        var safeId = NormalizeProfileId(profileId);
        return new PulseProfilePaths(Path.Combine(ProfilesRoot(), safeId));
    }

    public static PulseProfilePaths FromDirectory(string profileDir) =>
        new(profileDir);

    public static string NormalizeProfileId(string? profileId)
    {
        var value = string.IsNullOrWhiteSpace(profileId) ? "default" : profileId.Trim();
        var cleaned = new string(value.Select(ch =>
            char.IsLetterOrDigit(ch) || ch is '-' or '_' ? char.ToLowerInvariant(ch) : '-').ToArray());
        cleaned = Regex.Replace(cleaned, "-{2,}", "-").Trim('-');
        return string.IsNullOrWhiteSpace(cleaned) ? "default" : cleaned;
    }
}
