using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;

namespace Lumora.WinUI;

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
    public bool FullScreenAutoHideChrome { get; set; } = true;
    public string WindowBackdrop { get; set; } = "solid";
    // "dark" (identite historique Lumora), "light" ou "system" (suit le reglage
    // clair/sombre de Windows au demarrage et a chaque changement de reglage).
    public string ThemeMode { get; set; } = "dark";
    public string AccentPalette { get; set; } = "lumora";
    public int WindowTransparency { get; set; } = 18;
    public string StartupMode { get; set; } = "restore";
    public string StartupUrl { get; set; } = string.Empty;
    public string SearchEngine { get; set; } = "google";
    // Suggestions locales de la barre d'adresse (onglets ouverts, favoris,
    // historique du profil). Activées par défaut : tout est calculé sur
    // l'appareil, la frappe n'est jamais envoyée à un service distant.
    public bool AddressBarSuggestionsEnabled { get; set; } = true;
    public bool CommandPaletteEnabled { get; set; } = true;
    public bool CommandPaletteOpenFromWebPages { get; set; }
    public bool CommandPaletteOpenFromTextFields { get; set; }
    public string NewTabTitle { get; set; } = BrandingText.ProductName;
    public string NewTabStyle { get; set; } = "signature";
    public bool NewTabFocusSearchOnOpen { get; set; }
    public bool NewTabShortcutsVisible { get; set; }
    public List<NewTabShortcut> NewTabShortcuts { get; set; } = [];
    // Verrouillage auto par défaut à 10 min (valeur présente dans le sélecteur).
    // Les profils existants conservent la valeur enregistrée dans leur ui-settings.
    public int SessionTimeoutMinutes { get; set; } = 10;
    public bool NetworkBlockerEnabled { get; set; } = true;
    public bool TelemetryBlockerEnabled { get; set; } = true;
    // SmartScreen vérifie la réputation des sites en envoyant chaque URL à
    // Microsoft : désactivé par défaut (philosophie locale-first).
    public bool SmartScreenEnabled { get; set; }

    // Renforcement anti-publicité (0.77) : popups (popunders, clics détournés
    // vers un domaine listé) et navigations de l'onglet vers un domaine listé.
    // Actifs par défaut : c'est la protection attendue d'un navigateur moderne ;
    // désactivables ici, et la whitelist par site les lève site par site.
    public bool PopupBlockerEnabled { get; set; } = true;
    public bool StrictAdBlockEnabled { get; set; } = true;
    public bool ParameterCleanerEnabled { get; set; } = true;
    public bool HttpsEnforcerEnabled { get; set; } = true;
    public bool CnameUncloakerEnabled { get; set; } = true;
    public bool CosmeticFilterEnabled   { get; set; } = true;
    public bool ConsentManagerEnabled   { get; set; } = true;
    public bool AccessibilityHighContrast { get; set; }
    public bool AccessibilityLargeText { get; set; }
    public bool AccessibilityReduceMotion { get; set; }
    public bool AccessibilityVisibleFocus { get; set; } = true;
    // Bouton micro de la barre d'adresse : simple aide-mémoire vers la dictée
    // Windows (Win+H), Lumora n'ouvre jamais le micro lui-même (le moteur SAPI
    // intégré des versions 0.65.x a été retiré : retranscription trop mauvaise).
    public bool AccessibilityVoiceDictationEnabled { get; set; }
    // Lecture a voix haute : synthese vocale 100% locale (Windows), aucun
    // texte de page envoye a un serveur externe. Fonction de confort, pas une
    // protection : opt-in comme le micro et l'assistant IA, pour ne pas encombrer
    // la barre d'adresse d'un bouton que l'utilisateur n'a pas demande. Les
    // protections (bloqueur pub/traqueurs, HTTPS, cookies...) restent, elles,
    // actives par defaut. L'utilisateur active la lecture dans les Reglages.
    public bool ReadAloudEnabled { get; set; }
    // Traduction neuronale locale des pages (modele telecharge une fois puis
    // traduction 100% hors ligne). Activee par defaut : simple telechargement
    // generique, pas de donnee personnelle envoyee, sur le meme principe que
    // les listes de filtrage du bloqueur de pubs/traqueurs.
    public bool TranslationEnabled { get; set; } = true;
    // Assistant IA local (Phi-3-mini) pour reformuler une recherche vague.
    // Desactive par defaut : telechargement unique ~2,7 Go, activation
    // explicite requise (contrairement a la traduction, poids nettement
    // plus lourd et fonctionnalite plus proche de l'experimental).
    public bool SearchAssistEnabled { get; set; }
    public List<string> PinnedModuleIds { get; set; } =
    [
        "reader",
        "notes",
        "readAloud",
        "videoDownload",
        "searchAssist"
    ];
    // Préférences du générateur de mots de passe du coffre, partagées entre le
    // dialogue "Nouvel identifiant" et la barre de suggestion automatique.
    public int VaultGeneratorLength { get; set; } = 20;
    public bool VaultGeneratorUseSymbols { get; set; } = true;
    public string VaultGeneratorMode { get; set; } = "random"; // "random" ou "passphrase"
    public int VaultGeneratorPassphraseWords { get; set; } = 5;
    public bool SetupWizardCompleted    { get; set; }
    public List<string> PrivacyWhitelist { get; set; } = new();
    // Mode compatibilite connexion : domaines racines ou l'utilisateur autorise
    // Lumora a relacher uniquement les garde-fous qui cassent l'authentification
    // demandee (ex. Google Identity), sans ouvrir Ads/Analytics globalement.
    public List<string> LoginCompatibilitySites { get; set; } = new();
    public List<string> LoginDiagnosticSites { get; set; } = new();
    public List<SitePermissionRule> SitePermissions { get; set; } = new();
    // Sessions éphémères : au démarrage, purge des cookies de la session précédente,
    // sauf pour les domaines racines listés comme sites de confiance.
    public bool SessionPurgeEnabled { get; set; } = true;
    public List<string> TrustedSessionSites { get; set; } = new();
    // Vrai dès qu'une purge réelle a été expliquée une première fois à l'utilisateur
    // (InfoBar affichée une seule fois, jamais republiée automatiquement ensuite).
    public bool SessionPurgeExplained { get; set; }
    // Domaines racines pour lesquels l'utilisateur a refusé la proposition « Rester
    // connecté ? » au login : on ne le lui repropose plus à chaque connexion.
    public List<string> SessionKeepDeclinedSites { get; set; } = new();

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
                    migrated.ApplyLegacyBrandingMigration();
                    migrated.RemoveLegacyDefaultShortcuts();
                    migrated.Save(path);
                    try { File.Delete(legacyPath); } catch { }
                    return migrated;
                }

                return Default();
            }

            var json = LumoraFile.TryReadAllText(path);
            if (json is null) return Default();
            var settings = JsonSerializer.Deserialize<UiSettings>(json, JsonOptions) ?? Default();
            settings.ApplyLegacyBrandingMigration();
            settings.RemoveLegacyDefaultShortcuts();
            return settings;
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
            LumoraFile.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception error)
        {
            WinUiRuntimeTrace.Write($"UI settings save skipped: {error.GetType().Name}");
        }
    }

    private void RemoveLegacyDefaultShortcuts()
    {
        if (NewTabShortcuts.Count != 4) return;

        var legacyDefaults = new[]
        {
            new NewTabShortcut("Accueil", "lumora://accueil"),
            new NewTabShortcut("Google", "https://www.google.com"),
            new NewTabShortcut("YouTube", "https://www.youtube.com"),
            new NewTabShortcut("GitHub", "https://github.com")
        };

        for (var i = 0; i < legacyDefaults.Length; i++)
        {
            if (!string.Equals(NewTabShortcuts[i].Title, legacyDefaults[i].Title, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(NewTabShortcuts[i].Url, legacyDefaults[i].Url, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        NewTabShortcuts.Clear();
        NewTabShortcutsVisible = false;
    }

    private void ApplyLegacyBrandingMigration()
    {
        NewTabTitle = BrandingText.NormalizeLegacyProductTitle(NewTabTitle);
    }
}

internal sealed record NewTabShortcut(string Title, string Url);
