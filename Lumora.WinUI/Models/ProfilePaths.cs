using System.Text.RegularExpressions;

namespace Lumora.WinUI;

internal sealed class LumoraProfilePaths
{
    public const string ProfileDirectoryEnvironmentVariable = "LUMORA_PROFILE_DIR";
    private const string LegacyNovaProfileDirectoryEnvironmentVariable = "NOVA_BROWSER_PROFILE_DIR";
    private const string LegacyPulseProfileDirectoryEnvironmentVariable = "PULSE_BROWSER_PROFILE_DIR";

    private LumoraProfilePaths(string profileDir)
    {
        ProfileDir          = profileDir;
        NavigationDir       = Path.Combine(profileDir, "navigation");
        BookmarksFile       = DataFile(NavigationDir, "bookmarks");
        LegacyBookmarksFile = Path.Combine(NavigationDir, "bookmarks.tsv");
        LegacyFavoritesFile = Path.Combine(NavigationDir, "favorites.tsv");
        FaviconsDir         = Path.Combine(NavigationDir, "favicons");
        UiSettingsFile      = DataFile(NavigationDir, "ui-settings");
        LegacyUiSettingsFile = Path.Combine(NavigationDir, "ui-settings.json");
        TabsFile            = DataFile(NavigationDir, "tabs");
        LegacyTabsFile      = Path.Combine(NavigationDir, "tabs.json");
        HistoryFile         = DataFile(NavigationDir, "history");
        LegacyHistoryFile   = Path.Combine(NavigationDir, "history.json");
        DownloadsFile       = DataFile(NavigationDir, "downloads");
        PasskeysFile        = DataFile(NavigationDir, "passkeys");
        VaultFile           = DataFile(profileDir, "vault");
        ProfileFile         = DataFile(profileDir, "profile");
        LegacyProfileFile   = Path.Combine(profileDir, "profile.json");
        BrowserDataDir      = Path.Combine(profileDir, "webview2");
        WebAppsFile         = DataFile(NavigationDir, "webapps");
        WebAppIconsDir      = Path.Combine(NavigationDir, "webapp-icons");
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
    public string DownloadsFile        { get; }
    public string PasskeysFile         { get; }
    public string VaultFile            { get; }
    public string ProfileFile          { get; }
    public string LegacyProfileFile    { get; }
    public string BrowserDataDir       { get; }
    public string WebAppsFile          { get; }
    public string WebAppIconsDir       { get; }

    public static string ProfilesRoot()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
            localAppData = Path.GetTempPath();

        return Path.Combine(localAppData, "Lumora", "profiles");
    }

    public static LumoraProfilePaths Default()
    {
        var environmentProfileDir =
            Environment.GetEnvironmentVariable(ProfileDirectoryEnvironmentVariable)
            ?? Environment.GetEnvironmentVariable(LegacyNovaProfileDirectoryEnvironmentVariable)
            ?? Environment.GetEnvironmentVariable(LegacyPulseProfileDirectoryEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentProfileDir))
        {
            return FromDirectory(Path.GetFullPath(environmentProfileDir));
        }

        var config = LumoraConfig.Load();
        if (!string.IsNullOrWhiteSpace(config.CustomProfilePath)
            && Directory.Exists(config.CustomProfilePath))
        {
            return new LumoraProfilePaths(config.CustomProfilePath);
        }

        return ForProfileId(config.ActiveProfileId);
    }

    public static LumoraProfilePaths ForProfileId(string? profileId)
    {
        var safeId = NormalizeProfileId(profileId);
        return new LumoraProfilePaths(Path.Combine(ProfilesRoot(), safeId));
    }

    public static LumoraProfilePaths FromDirectory(string profileDir) =>
        new(profileDir);

    public static string NormalizeProfileId(string? profileId)
    {
        var value = string.IsNullOrWhiteSpace(profileId) ? "default" : profileId.Trim();
        var cleaned = new string(value.Select(ch =>
            char.IsLetterOrDigit(ch) || ch is '-' or '_' ? char.ToLowerInvariant(ch) : '-').ToArray());
        cleaned = Regex.Replace(cleaned, "-{2,}", "-").Trim('-');
        return string.IsNullOrWhiteSpace(cleaned) ? "default" : cleaned;
    }

    private static string DataFile(string directory, string name)
    {
        var lumora = Path.Combine(directory, $"{name}.lumora");
        if (File.Exists(lumora)) return lumora;

        var legacyPulse = Path.Combine(directory, $"{name}.pulse");
        if (File.Exists(legacyPulse)) return legacyPulse;

        var legacyNova = Path.Combine(directory, $"{name}.nova");
        return File.Exists(legacyNova) ? legacyNova : lumora;
    }
}
