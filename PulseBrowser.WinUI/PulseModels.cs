using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;

namespace PulseBrowser.WinUI;

internal sealed record BrowserTabState(int Id, string Title, string Address)
{
    public string Title { get; set; } = Title;
    public string Address { get; set; } = Address;
    public string IconPath { get; set; } = string.Empty;
    public string IconUri => string.IsNullOrWhiteSpace(IconPath) ? string.Empty : new Uri(IconPath).AbsoluteUri;

    // Un WebView2 par onglet : chaque onglet garde son moteur (créé paresseusement
    // à la première activation) et l'adresse à charger dès que le moteur est prêt.
    public Microsoft.UI.Xaml.Controls.WebView2? View { get; set; }
    public string? PendingAddress { get; set; }
}

public sealed record BookmarkListItem(
    string IconGlyph,
    string IconImageUri,
    Visibility IconImageVisibility,
    Visibility IconGlyphVisibility,
    string Title,
    string Detail,
    BookmarkNode Node);

public enum BookmarkKind
{
    Folder,
    Url
}

public sealed record BookmarkNode(
    string Id,
    string ParentId,
    BookmarkKind Kind,
    uint Position,
    string Title,
    string Url,
    string IconPath = "")
{
    public bool IsRoot => Id is BookmarkStore.ToolbarRootId or BookmarkStore.OtherRootId;
    public string IconUri => string.IsNullOrWhiteSpace(IconPath) ? string.Empty : new Uri(IconPath).AbsoluteUri;
}

public static class BookmarkGlyphs
{
    public const string Folder = "";
    public const string Link = "";

    public static string For(BookmarkNode node) => node.Kind == BookmarkKind.Folder ? Folder : Link;
}

// ── Chiffrement DPAPI des fichiers .pulse ─────────────────────────────────────

internal static class PulseFile
{
    // Entropie spécifique au projet pour distinguer nos fichiers d'autres données DPAPI
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PulseBrowser.WinUI.v1");

    public static void WriteAllText(string path, string content)
    {
        var plain  = Encoding.UTF8.GetBytes(content);
        var cipher = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
        var dir    = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(path, cipher);
    }

    public static string ReadAllText(string path)
    {
        var cipher = File.ReadAllBytes(path);
        var plain  = ProtectedData.Unprotect(cipher, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plain);
    }

    public static string? TryReadAllText(string path)
    {
        try   { return File.Exists(path) ? ReadAllText(path) : null; }
        catch { return null; }
    }
}

// ── Chemins du profil local ───────────────────────────────────────────────────

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
    public static List<PulseProfileEntry> Discover(PulseConfig config)
    {
        var entries = new List<PulseProfileEntry>();
        var activeId = PulseProfilePaths.NormalizeProfileId(config.ActiveProfileId);

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
                    true,
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
                    string.Equals(id, activeId, StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(config.CustomProfilePath),
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

internal sealed class UiSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public bool BookmarksBarVisible { get; set; } = true;
    public bool VerticalTabsEnabled { get; set; }
    public bool VerticalTabsCompact { get; set; }
    public double VerticalTabsWidth { get; set; } = 230;
    public bool CompactModeEnabled { get; set; }
    public bool CompactModeHidesBookmarks { get; set; }
    public string WindowBackdrop { get; set; } = "solid";
    public int WindowTransparency { get; set; } = 18;
    public string StartupMode { get; set; } = "restore";
    public string StartupUrl { get; set; } = string.Empty;
    public string SearchEngine { get; set; } = "google";
    public bool CommandPaletteEnabled { get; set; } = true;
    public bool CommandPaletteOpenFromWebPages { get; set; }
    public bool CommandPaletteOpenFromTextFields { get; set; }
    public string NewTabTitle { get; set; } = "Pulse";
    public bool NewTabShortcutsVisible { get; set; } = true;
    public List<NewTabShortcut> NewTabShortcuts { get; set; } =
    [
        new("Accueil", "pulse://accueil"),
        new("Google", "https://www.google.com"),
        new("YouTube", "https://www.youtube.com"),
        new("GitHub", "https://github.com")
    ];
    public int SessionTimeoutMinutes { get; set; } = 0;
    public bool NetworkBlockerEnabled { get; set; } = true;
    public bool ParameterCleanerEnabled { get; set; } = true;
    public bool HttpsEnforcerEnabled { get; set; } = true;
    public bool CnameUncloakerEnabled { get; set; } = true;
    public bool CosmeticFilterEnabled   { get; set; } = true;
    public bool ConsentManagerEnabled   { get; set; } = true;
    public bool AccessibilityHighContrast { get; set; }
    public bool AccessibilityLargeText { get; set; }
    public bool AccessibilityReduceMotion { get; set; }
    public bool AccessibilityVisibleFocus { get; set; } = true;
    public bool SetupWizardCompleted    { get; set; }
    public List<string> PrivacyWhitelist { get; set; } = new();
    // Sessions éphémères : au démarrage, purge des cookies de la session précédente,
    // sauf pour les domaines racines listés comme sites de confiance.
    public bool SessionPurgeEnabled { get; set; } = true;
    public List<string> TrustedSessionSites { get; set; } = new();

    public static UiSettings Default() => new();

    public static UiSettings Load(string path, string? legacyPath = null)
    {
        try
        {
            if (!File.Exists(path))
            {
                // Migration depuis .json legacy
                if (legacyPath is not null && File.Exists(legacyPath))
                {
                    var migrated = JsonSerializer.Deserialize<UiSettings>(File.ReadAllText(legacyPath), JsonOptions) ?? Default();
                    migrated.Save(path);
                    try { File.Delete(legacyPath); } catch { }
                    return migrated;
                }

                return Default();
            }

            var json = PulseFile.TryReadAllText(path);
            if (json is null) return Default();
            return JsonSerializer.Deserialize<UiSettings>(json, JsonOptions) ?? Default();
        }
        catch (Exception error)
        {
            WinUiRuntimeTrace.Write($"UI settings load skipped: {error.GetType().Name}");
            return Default();
        }
    }

    public void Save(string path)
    {
        try
        {
            PulseFile.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception error)
        {
            WinUiRuntimeTrace.Write($"UI settings save skipped: {error.GetType().Name}");
        }
    }
}

internal sealed record NewTabShortcut(string Title, string Url);

public sealed class BookmarkStore
{
    public const string ToolbarRootId = "root-toolbar";
    public const string OtherRootId = "root-other";

    private readonly string _bookmarksFile;
    private readonly string? _legacyBookmarksFile;
    private readonly string? _legacyFavoritesFile;
    private bool _isGuest;

    public BookmarkStore(string bookmarksFile, string? legacyBookmarksFile, string? legacyFavoritesFile)
    {
        _bookmarksFile = bookmarksFile;
        _legacyBookmarksFile = legacyBookmarksFile;
        _legacyFavoritesFile = legacyFavoritesFile;
    }

    public void SetGuestMode(bool isGuest) => _isGuest = isGuest;

    public List<BookmarkNode> AllNodes()
    {
        if (_isGuest) return Sort(RootNodes());

        EnsureFile();
        var content = PulseFile.TryReadAllText(_bookmarksFile) ?? string.Empty;
        var nodes = content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseLine)
            .Where(node => node is not null)
            .Cast<BookmarkNode>()
            .ToList();
        EnsureRoots(nodes);
        return Sort(nodes);
    }

    public void AddUrl(string parentId, string url, string title, string iconPath = "")
    {
        if (!IsWebUrl(url))
        {
            return;
        }

        var nodes = AllNodes();
        nodes.Add(new BookmarkNode(NextNodeId(nodes, "bookmark"), parentId, BookmarkKind.Url, NextPosition(nodes, parentId), CleanTitle(title, url), url, CleanLocalPath(iconPath)));
        WriteNodes(nodes);
    }

    public void AddOrUpdateUrl(string? id, string parentId, string url, string title, string iconPath = "")
    {
        if (!IsWebUrl(url))
        {
            return;
        }

        var nodes = AllNodes();
        if (nodes.All(node => node.Id != parentId || node.Kind != BookmarkKind.Folder))
        {
            parentId = ToolbarRootId;
        }

        var index = string.IsNullOrWhiteSpace(id)
            ? -1
            : nodes.FindIndex(node => node.Id == id && node.Kind == BookmarkKind.Url);

        if (index >= 0)
        {
            var node = nodes[index];
            var moved = !node.ParentId.Equals(parentId, StringComparison.Ordinal);
            nodes[index] = node with
            {
                ParentId = parentId,
                Position = moved ? NextPosition(nodes, parentId) : node.Position,
                Title = CleanTitle(title, url),
                Url = url,
                IconPath = CleanLocalPath(iconPath)
            };
            WriteNodes(nodes);
            return;
        }

        nodes.Add(new BookmarkNode(
            NextNodeId(nodes, "bookmark"),
            parentId,
            BookmarkKind.Url,
            NextPosition(nodes, parentId),
            CleanTitle(title, url),
            url,
            CleanLocalPath(iconPath)));
        WriteNodes(nodes);
    }

    public bool SetIconForUrl(string url, string iconPath)
    {
        if (!IsWebUrl(url) || string.IsNullOrWhiteSpace(iconPath))
        {
            return false;
        }

        var nodes = AllNodes();
        var changed = false;
        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            if (node.Kind == BookmarkKind.Url && node.Url.Equals(url, StringComparison.OrdinalIgnoreCase) && node.IconPath != iconPath)
            {
                nodes[index] = node with { IconPath = CleanLocalPath(iconPath) };
                changed = true;
            }
        }

        if (changed)
        {
            WriteNodes(nodes);
        }

        return changed;
    }

    public bool SetIconForOrigin(string url, string iconPath)
    {
        if (!IsWebUrl(url) || string.IsNullOrWhiteSpace(iconPath))
        {
            return false;
        }

        var origin = UrlOrigin(url);
        var nodes = AllNodes();
        var changed = false;
        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            if (node.Kind == BookmarkKind.Url && node.IconPath != iconPath &&
                UrlOrigin(node.Url).Equals(origin, StringComparison.OrdinalIgnoreCase))
            {
                nodes[index] = node with { IconPath = CleanLocalPath(iconPath) };
                changed = true;
            }
        }

        if (changed)
        {
            WriteNodes(nodes);
        }

        return changed;
    }

    private static string UrlOrigin(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return url;
        var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
        return $"{uri.Scheme}://{host}";
    }

    public void AddFolder(string parentId, string title)
    {
        var nodes = AllNodes();
        if (nodes.All(node => node.Id != parentId || node.Kind != BookmarkKind.Folder))
        {
            parentId = ToolbarRootId;
        }

        nodes.Add(new BookmarkNode(NextNodeId(nodes, "folder"), parentId, BookmarkKind.Folder, NextPosition(nodes, parentId), CleanTitle(title, string.Empty), string.Empty));
        WriteNodes(nodes);
    }

    public void RenameNode(string id, string title)
    {
        if (id is ToolbarRootId or OtherRootId)
        {
            return;
        }

        var nodes = AllNodes();
        var index = nodes.FindIndex(node => node.Id == id);
        if (index < 0)
        {
            return;
        }

        var node = nodes[index];
        nodes[index] = node with { Title = CleanTitle(title, node.Url) };
        WriteNodes(nodes);
    }

    public void RemoveNode(string id)
    {
        if (id is ToolbarRootId or OtherRootId)
        {
            return;
        }

        var nodes = AllNodes();
        var removeIds = new HashSet<string> { id };
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var node in nodes)
            {
                if (removeIds.Contains(node.ParentId) && removeIds.Add(node.Id))
                {
                    changed = true;
                }
            }
        }

        WriteNodes(nodes.Where(node => !removeIds.Contains(node.Id)).ToList());
    }

    public int MergeImport(BookmarkImportTree tree)
    {
        EnsureFile();
        Backup("before-winui-import");
        var nodes = AllNodes();
        var imported = 0;
        imported += AddImportItems(nodes, ToolbarRootId, tree.Toolbar);
        imported += AddImportItems(nodes, OtherRootId, tree.Other);
        WriteNodes(nodes);
        return imported;
    }

    public int ReplaceWithImport(BookmarkImportTree tree)
    {
        EnsureFile();
        Backup("before-winui-replace");
        var nodes = RootNodes();
        var imported = 0;
        imported += AddImportItems(nodes, ToolbarRootId, tree.Toolbar);
        imported += AddImportItems(nodes, OtherRootId, tree.Other);
        WriteNodes(nodes);
        return imported;
    }

    private void EnsureFile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_bookmarksFile)!);
        if (File.Exists(_bookmarksFile)) return;

        var nodes = RootNodes();

        // Migration depuis bookmarks.tsv (format précédent non chiffré)
        if (_legacyBookmarksFile is not null && File.Exists(_legacyBookmarksFile))
        {
            var legacyContent = File.ReadAllText(_legacyBookmarksFile, Encoding.UTF8);
            PulseFile.WriteAllText(_bookmarksFile, legacyContent);
            try { File.Delete(_legacyBookmarksFile); } catch { }
            return;
        }

        // Migration depuis favorites.tsv (très ancien format plat)
        if (_legacyFavoritesFile is not null && File.Exists(_legacyFavoritesFile))
        {
            var folderId = "legacy-flat-favorites";
            nodes.Add(new BookmarkNode(folderId, OtherRootId, BookmarkKind.Folder, 0, "Anciens favoris importes", string.Empty));
            var index = 0u;
            foreach (var line in File.ReadLines(_legacyFavoritesFile))
            {
                var parts = line.Split('\t');
                if (parts.Length < 3) continue;
                var url = DecodeField(parts[1]);
                if (!IsWebUrl(url)) continue;
                nodes.Add(new BookmarkNode($"legacy-{index + 1}", folderId, BookmarkKind.Url, index++, CleanTitle(DecodeField(parts[2]), url), url));
            }
        }

        WriteNodes(nodes);
    }

    private int AddImportItems(List<BookmarkNode> nodes, string parentId, IReadOnlyList<BookmarkImportItem> items)
    {
        var imported = 0;
        foreach (var item in items)
        {
            if (item.Url is not null)
            {
                if (!IsWebUrl(item.Url) || nodes.Any(node => node.Kind == BookmarkKind.Url && node.Url == item.Url))
                {
                    continue;
                }

                nodes.Add(new BookmarkNode(NextNodeId(nodes, "bookmark"), parentId, BookmarkKind.Url, NextPosition(nodes, parentId), CleanTitle(item.Title, item.Url), item.Url));
                imported++;
                continue;
            }

            var folderId = NextNodeId(nodes, "folder");
            nodes.Add(new BookmarkNode(folderId, parentId, BookmarkKind.Folder, NextPosition(nodes, parentId), CleanTitle(item.Title, string.Empty), string.Empty));
            imported += AddImportItems(nodes, folderId, item.Children);
        }

        return imported;
    }

    private void WriteNodes(IReadOnlyList<BookmarkNode> nodes)
    {
        if (_isGuest) return;
        var builder = new StringBuilder();
        foreach (var node in Sort(nodes))
        {
            builder.Append(EncodeField(node.Id)).Append('\t')
                .Append(EncodeField(node.ParentId)).Append('\t')
                .Append(node.Kind == BookmarkKind.Folder ? "folder" : "url").Append('\t')
                .Append(node.Position).Append('\t')
                .Append(EncodeField(node.Title)).Append('\t')
                .Append(EncodeField(node.Url)).Append('\t')
                .Append(EncodeField(node.IconPath)).Append('\n');
        }

        PulseFile.WriteAllText(_bookmarksFile, builder.ToString());
    }

    private void Backup(string label)
    {
        if (!File.Exists(_bookmarksFile))
        {
            return;
        }

        var backupPath = Path.Combine(Path.GetDirectoryName(_bookmarksFile)!, $"bookmarks.{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.{label}.bak.tsv");
        File.Copy(_bookmarksFile, backupPath, overwrite: true);
    }

    private static List<BookmarkNode> RootNodes() =>
        new()
        {
            new BookmarkNode(ToolbarRootId, string.Empty, BookmarkKind.Folder, 0, "Barre des favoris", string.Empty),
            new BookmarkNode(OtherRootId, string.Empty, BookmarkKind.Folder, 1, "Autres favoris", string.Empty)
        };

    private static void EnsureRoots(List<BookmarkNode> nodes)
    {
        foreach (var root in RootNodes())
        {
            if (nodes.All(node => node.Id != root.Id))
            {
                nodes.Add(root);
            }
        }
    }

    private static BookmarkNode? ParseLine(string line)
    {
        var parts = line.Split('\t');
        if (parts.Length < 6 || !uint.TryParse(parts[3], out var position))
        {
            return null;
        }

        var kind = parts[2] switch
        {
            "folder" => BookmarkKind.Folder,
            "url" => BookmarkKind.Url,
            _ => (BookmarkKind?)null
        };

        var iconPath = parts.Length >= 7 ? CleanLocalPath(DecodeField(parts[6])) : string.Empty;
        return kind is null
            ? null
            : new BookmarkNode(DecodeField(parts[0]), DecodeField(parts[1]), kind.Value, position, DecodeField(parts[4]), DecodeField(parts[5]), iconPath);
    }

    private static List<BookmarkNode> Sort(IEnumerable<BookmarkNode> nodes) =>
        nodes.OrderBy(node => node.ParentId).ThenBy(node => node.Position).ThenBy(node => node.Title).ToList();

    private static uint NextPosition(IEnumerable<BookmarkNode> nodes, string parentId)
    {
        var positions = nodes.Where(node => node.ParentId == parentId).Select(node => node.Position).ToList();
        return positions.Count == 0 ? 0 : positions.Max() + 1;
    }

    private static string NextNodeId(IEnumerable<BookmarkNode> nodes, string prefix)
    {
        var existing = nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var index = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        while (true)
        {
            var id = $"{prefix}-{index++}";
            if (!existing.Contains(id))
            {
                return id;
            }
        }
    }

    private static string CleanTitle(string title, string url)
    {
        title = title.Trim();
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Length > 160 ? title[..160] : title;
        }

        return IsWebUrl(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "Dossier sans nom";
    }

    private static string CleanLocalPath(string path)
    {
        path = path.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return string.Empty;
        }
    }

    public static bool IsWebUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    private static string EncodeField(string value)
    {
        var builder = new StringBuilder();
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            if ((b >= 'A' && b <= 'Z') || (b >= 'a' && b <= 'z') || (b >= '0' && b <= '9') || b is (byte)'-' or (byte)'_' or (byte)'.' or (byte)'~')
            {
                builder.Append((char)b);
            }
            else
            {
                builder.Append('%').Append(b.ToString("X2"));
            }
        }

        return builder.ToString();
    }

    private static string DecodeField(string value)
    {
        var bytes = new List<byte>();
        for (var index = 0; index < value.Length;)
        {
            if (value[index] == '%' && index + 2 < value.Length && byte.TryParse(value.Substring(index + 1, 2), System.Globalization.NumberStyles.HexNumber, null, out var decoded))
            {
                bytes.Add(decoded);
                index += 3;
            }
            else
            {
                bytes.Add((byte)value[index]);
                index++;
            }
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }
}

public sealed record BookmarkImportItem(string Title, string? Url, List<BookmarkImportItem> Children)
{
    public static BookmarkImportItem Folder(string title, IEnumerable<BookmarkImportItem> children) =>
        new(title, null, children.ToList());

    public static BookmarkImportItem UrlItem(string title, string url) =>
        new(title, url, new List<BookmarkImportItem>());
}

public sealed record BookmarkImportTree(List<BookmarkImportItem> Toolbar, List<BookmarkImportItem> Other)
{
    private static readonly Regex AnchorRegex = new(
        "<A\\s+[^>]*HREF\\s*=\\s*[\"'](?<url>[^\"']+)[\"'][^>]*>(?<title>.*?)</A>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    public static BookmarkImportTree FromHtml(string html, string sourceName)
    {
        var items = AnchorRegex.Matches(html)
            .Select(match => BookmarkImportItem.UrlItem(
                WebUtility.HtmlDecode(Regex.Replace(match.Groups["title"].Value, "<.*?>", string.Empty)).Trim(),
                WebUtility.HtmlDecode(match.Groups["url"].Value).Trim()))
            .Where(item => BookmarkStore.IsWebUrl(item.Url ?? string.Empty))
            .ToList();

        return new BookmarkImportTree(new List<BookmarkImportItem>(), new List<BookmarkImportItem>
        {
            BookmarkImportItem.Folder($"Import HTML {sourceName}", items)
        });
    }
}

public sealed record MigrationBrowserEntry(string Name, BrowserImportSource? Source)
{
    public string Label => Source is not null
        ? $"{Name}  —  {Source.Count} favori(s) détecté(s)"
        : $"{Name}  —  non détecté";
}

public sealed record BrowserImportSource(string Browser, string Profile, string Path, int Count)
{
    public string Label => $"{Browser} {Profile} - {Count} favoris";

    public static List<BrowserImportSource> Discover()
    {
        var local   = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var roots = new (string Browser, string UserData)[]
        {
            ("Google Chrome", System.IO.Path.Combine(local,   "Google", "Chrome", "User Data")),
            ("Microsoft Edge", System.IO.Path.Combine(local,   "Microsoft", "Edge", "User Data")),
            ("Brave",          System.IO.Path.Combine(local,   "BraveSoftware", "Brave-Browser", "User Data")),
            ("Chromium",       System.IO.Path.Combine(local,   "Chromium", "User Data")),
            ("Vivaldi",        System.IO.Path.Combine(local,   "Vivaldi", "User Data")),
            ("Opera",          System.IO.Path.Combine(roaming, "Opera Software", "Opera Stable", "User Data")),
            ("Opera GX",       System.IO.Path.Combine(roaming, "Opera Software", "Opera GX Stable", "User Data")),
        };

        var sources = new List<BrowserImportSource>();
        foreach (var (browser, userData) in roots)
        {
            if (!Directory.Exists(userData))
            {
                continue;
            }

            foreach (var profileDir in Directory.EnumerateDirectories(userData).Where(IsChromiumProfileDir))
            {
                var bookmarks = System.IO.Path.Combine(profileDir, "Bookmarks");
                if (!File.Exists(bookmarks))
                {
                    continue;
                }

                var count = CountUrls(bookmarks);
                if (count > 0)
                {
                    sources.Add(new BrowserImportSource(browser, System.IO.Path.GetFileName(profileDir), bookmarks, count));
                }
            }
        }

        // Firefox : format SQLite (places.sqlite), lu par FirefoxBookmarkReader.
        foreach (var (profileName, placesPath) in FirefoxBookmarkReader.DiscoverProfiles())
        {
            var count = FirefoxBookmarkReader.CountUrls(placesPath);
            if (count > 0)
            {
                sources.Add(new BrowserImportSource("Firefox", profileName, placesPath, count));
            }
        }

        return sources;
    }

    private bool IsFirefoxSource =>
        System.IO.Path.GetFileName(Path).Equals("places.sqlite", StringComparison.OrdinalIgnoreCase);

    public BookmarkImportTree ReadTree()
    {
        if (IsFirefoxSource)
        {
            return FirefoxBookmarkReader.ReadTree(Path);
        }

        var json = JsonNode.Parse(File.ReadAllText(Path));
        var roots = json?["roots"];
        return new BookmarkImportTree(
            ReadChildren(roots?["bookmark_bar"]?["children"]),
            ReadChildren(roots?["other"]?["children"]));
    }

    private static bool IsChromiumProfileDir(string path)
    {
        var name = System.IO.Path.GetFileName(path);
        return name.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountUrls(string bookmarksFile)
    {
        try
        {
            var source = new BrowserImportSource(string.Empty, string.Empty, bookmarksFile, 0);
            var tree = source.ReadTree();
            return CountItems(tree.Toolbar) + CountItems(tree.Other);
        }
        catch
        {
            return 0;
        }
    }

    private static int CountItems(IEnumerable<BookmarkImportItem> items) =>
        items.Sum(item => item.Url is not null ? 1 : CountItems(item.Children));

    private static List<BookmarkImportItem> ReadChildren(JsonNode? children)
    {
        var items = new List<BookmarkImportItem>();
        if (children is not JsonArray array)
        {
            return items;
        }

        foreach (var child in array)
        {
            var type = child?["type"]?.GetValue<string>() ?? string.Empty;
            var name = child?["name"]?.GetValue<string>() ?? string.Empty;
            if (type == "url")
            {
                var url = child?["url"]?.GetValue<string>() ?? string.Empty;
                if (BookmarkStore.IsWebUrl(url))
                {
                    items.Add(BookmarkImportItem.UrlItem(name, url));
                }
            }
            else if (type == "folder")
            {
                items.Add(BookmarkImportItem.Folder(name, ReadChildren(child?["children"])));
            }
        }

        return items;
    }
}

public static class BookmarkTreePresenter
{
    public static IEnumerable<BookmarkListItem> FlattenFolders(IReadOnlyList<BookmarkNode> nodes)
    {
        foreach (var rootId in new[] { BookmarkStore.ToolbarRootId, BookmarkStore.OtherRootId })
        {
            var root = nodes.First(node => node.Id == rootId);
            yield return ItemForNode(nodes, root, 0);
            foreach (var item in FlattenFolderChildren(nodes, root.Id, 1))
            {
                yield return item;
            }
        }
    }

    public static BookmarkListItem ItemForNode(IReadOnlyList<BookmarkNode> nodes, BookmarkNode node, int depth)
    {
        var prefix = new string(' ', depth * 3);
        var icon = BookmarkGlyphs.For(node);
        var title = string.IsNullOrWhiteSpace(node.Title) ? "(sans nom)" : node.Title;
        var detail = node.Kind == BookmarkKind.Folder
            ? $"{nodes.Count(candidate => candidate.ParentId == node.Id)} element(s)"
            : node.Url;
        var hasIcon = node.Kind == BookmarkKind.Url && !string.IsNullOrWhiteSpace(node.IconPath) && File.Exists(node.IconPath);
        return new BookmarkListItem(
            icon,
            hasIcon ? node.IconUri : string.Empty,
            hasIcon ? Visibility.Visible : Visibility.Collapsed,
            hasIcon ? Visibility.Collapsed : Visibility.Visible,
            prefix + title,
            detail,
            node);
    }

    public static string Breadcrumb(IReadOnlyList<BookmarkNode> nodes, string folderId)
    {
        var parts = new List<string>();
        var currentId = folderId;
        while (!string.IsNullOrWhiteSpace(currentId))
        {
            var current = nodes.FirstOrDefault(node => node.Id == currentId);
            if (current is null)
            {
                break;
            }

            parts.Insert(0, current.Title);
            currentId = current.ParentId;
        }

        return string.Join(" > ", parts);
    }

    public static IEnumerable<BookmarkListItem> Search(IReadOnlyList<BookmarkNode> nodes, string query)
    {
        var normalized = query.Trim().ToLowerInvariant();
        return nodes
            .Where(node => !node.IsRoot)
            .Where(node =>
                node.Title.ToLowerInvariant().Contains(normalized) ||
                node.Url.ToLowerInvariant().Contains(normalized))
            .OrderBy(node => node.Kind == BookmarkKind.Url ? 1 : 0)
            .ThenBy(node => node.Title)
            .Select(node => ItemForNode(nodes, node, 0));
    }

    private static IEnumerable<BookmarkListItem> FlattenFolderChildren(IReadOnlyList<BookmarkNode> nodes, string parentId, int depth)
    {
        foreach (var node in nodes
                     .Where(candidate => candidate.ParentId == parentId && candidate.Kind == BookmarkKind.Folder)
                     .OrderBy(candidate => candidate.Position))
        {
            yield return ItemForNode(nodes, node, depth);
            foreach (var child in FlattenFolderChildren(nodes, node.Id, depth + 1))
            {
                yield return child;
            }
        }
    }
}

public static class BookmarkHtmlExporter
{
    public static string Export(IReadOnlyList<BookmarkNode> nodes)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE NETSCAPE-Bookmark-file-1>");
        builder.AppendLine("<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\">");
        builder.AppendLine("<TITLE>Bookmarks</TITLE>");
        builder.AppendLine("<H1>Bookmarks</H1>");
        builder.AppendLine("<DL><p>");
        WriteChildren(builder, nodes, BookmarkStore.ToolbarRootId, 1, "Barre des favoris");
        WriteChildren(builder, nodes, BookmarkStore.OtherRootId, 1, "Autres favoris");
        builder.AppendLine("</DL><p>");
        return builder.ToString();
    }

    private static void WriteChildren(StringBuilder builder, IReadOnlyList<BookmarkNode> nodes, string parentId, int depth, string title)
    {
        var indent = new string(' ', depth * 4);
        builder.Append(indent).Append("<DT><H3>").Append(WebUtility.HtmlEncode(title)).AppendLine("</H3>");
        builder.Append(indent).AppendLine("<DL><p>");
        foreach (var node in nodes.Where(candidate => candidate.ParentId == parentId).OrderBy(candidate => candidate.Position))
        {
            if (node.Kind == BookmarkKind.Url)
            {
                builder.Append(indent).Append("    <DT><A HREF=\"")
                    .Append(WebUtility.HtmlEncode(node.Url))
                    .Append("\">")
                    .Append(WebUtility.HtmlEncode(node.Title))
                    .AppendLine("</A>");
            }
            else
            {
                WriteChildren(builder, nodes, node.Id, depth + 1, node.Title);
            }
        }

        builder.Append(indent).AppendLine("</DL><p>");
    }
}

internal sealed record SavedTab(string Title, string Address, string IconPath);

internal sealed class TabSession
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public List<SavedTab> Tabs { get; set; } = new();
    public int ActiveIndex { get; set; }

    public static TabSession Load(string path, string? legacyPath = null)
    {
        try
        {
            if (!File.Exists(path))
            {
                // Migration depuis tabs.json non chiffré
                if (legacyPath is not null && File.Exists(legacyPath))
                {
                    var migrated = JsonSerializer.Deserialize<TabSession>(File.ReadAllText(legacyPath), JsonOpts) ?? new TabSession();
                    migrated.Save(path);
                    try { File.Delete(legacyPath); } catch { }
                    return migrated;
                }

                return new TabSession();
            }

            var json = PulseFile.TryReadAllText(path);
            if (json is null) return new TabSession();
            return JsonSerializer.Deserialize<TabSession>(json, JsonOpts) ?? new TabSession();
        }
        catch
        {
            return new TabSession();
        }
    }

    public void Save(string path)
    {
        try
        {
            PulseFile.WriteAllText(path, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch { }
    }
}

public sealed record PasskeyEntry(string Origin, DateTimeOffset CreatedAt, DateTimeOffset LastUsedAt)
{
    public string Serialize() =>
        $"{Uri.EscapeDataString(Origin)}\t{CreatedAt.ToUnixTimeSeconds()}\t{LastUsedAt.ToUnixTimeSeconds()}";

    public static PasskeyEntry? TryParse(string line)
    {
        var parts = line.Split('\t');
        if (parts.Length < 3) return null;
        if (!long.TryParse(parts[1], out var created) || !long.TryParse(parts[2], out var used)) return null;
        return new PasskeyEntry(Uri.UnescapeDataString(parts[0]),
            DateTimeOffset.FromUnixTimeSeconds(created),
            DateTimeOffset.FromUnixTimeSeconds(used));
    }
}

public sealed record HistoryEntry(string Url, string Title, DateTimeOffset VisitedAt);

public sealed record HistoryListItem(
    string IconGlyph,
    string IconImageUri,
    Visibility IconImageVisibility,
    Visibility IconGlyphVisibility,
    string Title,
    string Domain,
    string TimeDisplay,
    HistoryEntry Entry);

internal sealed class HistoryStore
{
    private const int MaxEntries = 2000;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };
    private readonly string _historyFile;
    private readonly string? _legacyHistoryFile;
    private readonly List<HistoryEntry> _entries = new();
    private bool _isGuest;

    public void SetGuestMode(bool isGuest)
    {
        _isGuest = isGuest;
        if (isGuest) _entries.Clear(); // vider sans toucher le disque
    }

    public HistoryStore(string historyFile, string? legacyHistoryFile = null)
    {
        _historyFile       = historyFile;
        _legacyHistoryFile = legacyHistoryFile;
        Load();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_historyFile))
            {
                // Migration depuis history.json non chiffré
                if (_legacyHistoryFile is not null && File.Exists(_legacyHistoryFile))
                {
                    var legacyJson = File.ReadAllText(_legacyHistoryFile, Encoding.UTF8);
                    var legacyData = JsonSerializer.Deserialize<List<HistoryEntry>>(legacyJson, JsonOpts);
                    if (legacyData is not null) _entries.AddRange(legacyData);
                    Save(); // réécrit dans le nouveau format chiffré
                    try { File.Delete(_legacyHistoryFile); } catch { }
                }
                return;
            }

            var json = PulseFile.TryReadAllText(_historyFile);
            if (json is null) return;
            var loaded = JsonSerializer.Deserialize<List<HistoryEntry>>(json, JsonOpts);
            if (loaded is not null) _entries.AddRange(loaded);
        }
        catch { }
    }

    public void Add(string url, string title)
    {
        if (!BookmarkStore.IsWebUrl(url))
        {
            return;
        }

        _entries.Insert(0, new HistoryEntry(
            url,
            string.IsNullOrWhiteSpace(title) ? url : title,
            DateTimeOffset.Now));
        while (_entries.Count > MaxEntries)
        {
            _entries.RemoveAt(_entries.Count - 1);
        }

        Save();
    }

    public void Remove(HistoryEntry entry)
    {
        _entries.Remove(entry);
        Save();
    }

    public IReadOnlyList<HistoryEntry> AllEntries() => _entries;

    public IEnumerable<HistoryEntry> Search(string query)
    {
        var q = query.Trim().ToLowerInvariant();
        return _entries.Where(e =>
            e.Title.ToLowerInvariant().Contains(q) ||
            e.Url.ToLowerInvariant().Contains(q));
    }

    public void Clear()
    {
        _entries.Clear();
        Save();
    }

    private void Save()
    {
        if (_isGuest) return;
        try
        {
            PulseFile.WriteAllText(_historyFile, JsonSerializer.Serialize(_entries, JsonOpts));
        }
        catch { }
    }
}

internal sealed class DownloadEntry
{
    private long _totalBytes;
    private long _receivedBytes;
    private string _state = "En cours";

    public string FileName { get; }
    public string SourceDomain { get; }
    public string LocalPath { get; }
    public string State => _state;
    public long TotalBytes => _totalBytes;
    public long ReceivedBytes => _receivedBytes;
    public bool IsCompleted => _state == "Termine";
    public bool IsFailed => _state is "Echec" or "Annule";
    public double ProgressPercent => _totalBytes > 0 ? (double)_receivedBytes / _totalBytes * 100 : 0;

    public event Action? OnChanged;

    public DownloadEntry(CoreWebView2DownloadOperation operation)
    {
        LocalPath = operation.ResultFilePath;
        FileName = Path.GetFileName(LocalPath);
        SourceDomain = Uri.TryCreate(operation.Uri, UriKind.Absolute, out var uri)
            ? uri.Host
            : operation.Uri;
        _totalBytes = operation.TotalBytesToReceive > 0 ? operation.TotalBytesToReceive : 0;
        _receivedBytes = operation.BytesReceived;

        operation.StateChanged += (s, _) =>
        {
            _state = s.State switch
            {
                CoreWebView2DownloadState.Completed => "Termine",
                CoreWebView2DownloadState.Interrupted => "Echec",
                _ => "En cours"
            };
            OnChanged?.Invoke();
        };
        operation.BytesReceivedChanged += (s, _) =>
        {
            if (s.TotalBytesToReceive > 0) _totalBytes = s.TotalBytesToReceive;
            _receivedBytes = s.BytesReceived;
            OnChanged?.Invoke();
        };
    }
}

// ── Coffre local : modèle d'identifiant du coffre vault.pulse ────────────────

public sealed class VaultCredential
{
    [JsonPropertyName("origin")] public string Origin { get; init; } = string.Empty;
    [JsonPropertyName("username")] public string Username { get; init; } = string.Empty;
    [JsonPropertyName("password")] public string Password { get; init; } = string.Empty;
    // Nom personnalisé optionnel (édité par l'utilisateur), affiché en titre à la place de l'URL.
    [JsonPropertyName("label")] public string Label { get; init; } = string.Empty;
    // URL de la page où le mot de passe a été saisi, pour y retourner depuis le coffre
    // (comme les grands gestionnaires). Vide sur les anciennes entrées → repli sur Origin.
    [JsonPropertyName("login_url")] public string LoginUrl { get; init; } = string.Empty;
    [JsonPropertyName("created_at")] public long CreatedAt { get; init; }
    [JsonPropertyName("updated_at")] public long UpdatedAt { get; init; }
}

// ── Profil utilisateur local ──────────────────────────────────────────────────

internal sealed record UserProfile
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public string Name           { get; init; } = string.Empty;
    public string PasswordHash   { get; init; } = string.Empty;
    public string PasswordSalt   { get; init; } = string.Empty;
    public bool   HasPinLogin    { get; init; }
    public string PinHash        { get; init; } = string.Empty;
    public string PinSalt        { get; init; } = string.Empty;
    public string RecoveryHash   { get; init; } = string.Empty;
    public string RecoverySalt   { get; init; } = string.Empty;
    public long   RecoveryCreatedAt { get; init; }

    public bool HasRecoveryKey => !string.IsNullOrWhiteSpace(RecoveryHash) && !string.IsNullOrWhiteSpace(RecoverySalt);

    // ── Fabrique ─────────────────────────────────────────────────────────────

    public static UserProfile Create(string name, string password, string? pin)
    {
        var (pwHash, pwSalt) = DeriveKey(password);
        var hasPinLogin = pin is not null;
        var (pinHash, pinSalt) = hasPinLogin ? DeriveKey(pin!) : (string.Empty, string.Empty);

        return new UserProfile
        {
            Name         = name,
            PasswordHash = pwHash,
            PasswordSalt = pwSalt,
            HasPinLogin  = hasPinLogin,
            PinHash      = pinHash,
            PinSalt      = pinSalt
        };
    }

    // ── Vérification ─────────────────────────────────────────────────────────

    public bool VerifyPassword(string password)
    {
        if (string.IsNullOrEmpty(PasswordSalt)) return false;
        var salt = Convert.FromBase64String(PasswordSalt);
        var candidate = Pbkdf2(password, salt);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(PasswordHash), candidate);
    }

    public bool VerifyPin(string pin)
    {
        if (!HasPinLogin || string.IsNullOrEmpty(PinSalt)) return false;
        var salt = Convert.FromBase64String(PinSalt);
        var candidate = Pbkdf2(pin, salt);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(PinHash), candidate);
    }

    public bool VerifyRecoveryKey(string recoveryKey)
    {
        if (!HasRecoveryKey) return false;
        try
        {
            var salt = Convert.FromBase64String(RecoverySalt);
            var candidate = Pbkdf2(NormalizeRecoveryKey(recoveryKey), salt);
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(RecoveryHash), candidate);
        }
        catch
        {
            return false;
        }
    }

    // ── Mutations ────────────────────────────────────────────────────────────

    public UserProfile WithNewPassword(string newPassword)
    {
        var (hash, salt) = DeriveKey(newPassword);
        return this with { PasswordHash = hash, PasswordSalt = salt };
    }

    public UserProfile WithRecoveryKey(string recoveryKey)
    {
        var (hash, salt) = DeriveKey(NormalizeRecoveryKey(recoveryKey));
        return this with
        {
            RecoveryHash = hash,
            RecoverySalt = salt,
            RecoveryCreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }

    public UserProfile WithPin(string pin)
    {
        var (hash, salt) = DeriveKey(pin);
        return this with { HasPinLogin = true, PinHash = hash, PinSalt = salt };
    }

    public UserProfile WithoutPin() =>
        this with { HasPinLogin = false, PinHash = string.Empty, PinSalt = string.Empty };

    public static string GenerateRecoveryKey()
    {
        var hex = Convert.ToHexString(RandomNumberGenerator.GetBytes(18));
        var chunks = Enumerable.Range(0, hex.Length / 6)
            .Select(index => hex.Substring(index * 6, 6));
        return "PULSE-" + string.Join("-", chunks);
    }

    // ── Persistance DPAPI ─────────────────────────────────────────────────────

    public void Save(string path)
    {
        var json   = JsonSerializer.SerializeToUtf8Bytes(this, JsonOpts);
        var cipher = ProtectedData.Protect(json, null, DataProtectionScope.CurrentUser);
        var dir    = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(path, cipher);
    }

    public static UserProfile? Load(string path, string? legacyPath = null)
    {
        try
        {
            // Migration depuis profile.json (même format DPAPI, juste renommé)
            if (!File.Exists(path) && legacyPath is not null && File.Exists(legacyPath))
            {
                var cipher2 = File.ReadAllBytes(legacyPath);
                var plain2  = ProtectedData.Unprotect(cipher2, null, DataProtectionScope.CurrentUser);
                var profile = JsonSerializer.Deserialize<UserProfile>(plain2, JsonOpts);
                profile?.Save(path);
                try { File.Delete(legacyPath); } catch { }
                return profile;
            }

            if (!File.Exists(path)) return null;
            var cipher = File.ReadAllBytes(path);
            var plain  = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<UserProfile>(plain, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    // ── PBKDF2 (100 000 itérations, SHA-256, 32 octets) ─────────────────────

    private static (string hash, string salt) DeriveKey(string secret)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(32);
        var hashBytes = Pbkdf2(secret, saltBytes);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    private static byte[] Pbkdf2(string secret, byte[] salt)
    {
        using var rfc = new Rfc2898DeriveBytes(secret, salt, 100_000, HashAlgorithmName.SHA256);
        return rfc.GetBytes(32);
    }

    private static string NormalizeRecoveryKey(string recoveryKey) =>
        new(recoveryKey
            .Trim()
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
}
