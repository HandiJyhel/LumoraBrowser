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

    // Nombre d'itérations PBKDF2 réellement utilisé pour chaque secret. Absent dans
    // les anciens profils → LegacyPbkdf2Iterations (100k), qui restent donc vérifiables.
    // Tout secret créé ou modifié est ré-haché à Pbkdf2Iterations (600k).
    public int PasswordIterations { get; init; } = LegacyPbkdf2Iterations;
    public int PinIterations      { get; init; } = LegacyPbkdf2Iterations;
    public int RecoveryIterations { get; init; } = LegacyPbkdf2Iterations;

    public bool HasRecoveryKey => !string.IsNullOrWhiteSpace(RecoveryHash) && !string.IsNullOrWhiteSpace(RecoverySalt);

    // ── Fabrique ─────────────────────────────────────────────────────────────

    public static UserProfile Create(string name, string password, string? pin)
    {
        var (pwHash, pwSalt) = DeriveKey(password);
        var hasPinLogin = pin is not null;
        var (pinHash, pinSalt) = hasPinLogin ? DeriveKey(pin!) : (string.Empty, string.Empty);

        return new UserProfile
        {
            Name               = name,
            PasswordHash       = pwHash,
            PasswordSalt       = pwSalt,
            PasswordIterations = Pbkdf2Iterations,
            HasPinLogin        = hasPinLogin,
            PinHash            = pinHash,
            PinSalt            = pinSalt,
            PinIterations      = Pbkdf2Iterations
        };
    }

    // ── Vérification ─────────────────────────────────────────────────────────

    public bool VerifyPassword(string password)
    {
        if (string.IsNullOrEmpty(PasswordSalt)) return false;
        var salt = Convert.FromBase64String(PasswordSalt);
        var candidate = Pbkdf2(password, salt, PasswordIterations);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(PasswordHash), candidate);
    }

    public bool VerifyPin(string pin)
    {
        if (!HasPinLogin || string.IsNullOrEmpty(PinSalt)) return false;
        var salt = Convert.FromBase64String(PinSalt);
        var candidate = Pbkdf2(pin, salt, PinIterations);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(PinHash), candidate);
    }

    public bool VerifyRecoveryKey(string recoveryKey)
    {
        if (!HasRecoveryKey) return false;
        try
        {
            var salt = Convert.FromBase64String(RecoverySalt);
            var candidate = Pbkdf2(NormalizeRecoveryKey(recoveryKey), salt, RecoveryIterations);
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
        return this with { PasswordHash = hash, PasswordSalt = salt, PasswordIterations = Pbkdf2Iterations };
    }

    public UserProfile WithRecoveryKey(string recoveryKey)
    {
        var (hash, salt) = DeriveKey(NormalizeRecoveryKey(recoveryKey));
        return this with
        {
            RecoveryHash = hash,
            RecoverySalt = salt,
            RecoveryIterations = Pbkdf2Iterations,
            RecoveryCreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }

    public UserProfile WithPin(string pin)
    {
        var (hash, salt) = DeriveKey(pin);
        return this with { HasPinLogin = true, PinHash = hash, PinSalt = salt, PinIterations = Pbkdf2Iterations };
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

    // ── PBKDF2 (SHA-256, 32 octets) ─────────────────────────────────────────
    // Nouveaux secrets : 600 000 itérations (recommandation OWASP). Les anciens
    // profils gardent 100 000 (compteur stocké par secret) pour rester ouvrables,
    // et sont ré-haché à 600k dès qu'on modifie le secret concerné.

    private const int Pbkdf2Iterations = 600_000;
    private const int LegacyPbkdf2Iterations = 100_000;

    private static (string hash, string salt) DeriveKey(string secret)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(32);
        var hashBytes = Pbkdf2(secret, saltBytes, Pbkdf2Iterations);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    private static byte[] Pbkdf2(string secret, byte[] salt, int iterations)
    {
        using var rfc = new Rfc2898DeriveBytes(secret, salt, iterations, HashAlgorithmName.SHA256);
        return rfc.GetBytes(32);
    }

    private static string NormalizeRecoveryKey(string recoveryKey) =>
        new(recoveryKey
            .Trim()
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
}
