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
    // Verrouillage auto par défaut à 10 min (valeur présente dans le sélecteur).
    // Les profils existants conservent la valeur enregistrée dans leur ui-settings.
    public int SessionTimeoutMinutes { get; set; } = 10;
    public bool NetworkBlockerEnabled { get; set; } = true;
    public bool TelemetryBlockerEnabled { get; set; } = true;
    // SmartScreen vérifie la réputation des sites en envoyant chaque URL à
    // Microsoft : désactivé par défaut (philosophie locale-first).
    public bool SmartScreenEnabled { get; set; }
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

