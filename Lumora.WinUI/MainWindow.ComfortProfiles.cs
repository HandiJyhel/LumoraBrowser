using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    private sealed record AccessibilityComfortProfilePreset(
        string Key,
        string Label,
        string Summary,
        bool HighContrast,
        bool LargeText,
        bool ReduceMotion,
        bool VisibleFocus,
        bool ReduceBlueLight,
        bool ReadAloud,
        bool ReadingLens,
        bool ReadingGuide);

    // La cle "balanced" du premier preset est un nom technique herite, garde
    // tel quel pour ne rien casser dans les reglages deja sauvegardes sur
    // disque des utilisateurs. Son libelle affiche ("Aucune aide") a ete
    // corrige separement le 2026-07-20 : il entrait en collision avec le mode
    // de navigation "Equilibre" (MainWindow.UsageMode.cs) - meme nom, meme cle
    // brute "balanced", mais deux systemes de reglages totalement disjoints.
    // Ce preset ne configure d'ailleurs rien du tout (voir les valeurs plus
    // bas) : c'est l'etat de depart, pas un vrai profil d'aides.
    // "calm" (un seul reglage, ReduceMotion) et "reading" (combinaison lecture
    // vocale + loupe + guide simultanes, peu representative d'un vrai besoin)
    // retires le 2026-07-20 a la demande de l'utilisateur : un "profil" qui ne
    // fait qu'un seul reglage n'est pas un profil, et une combinaison
    // artificielle n'aide personne. Les cles restent reservees (non reutilisees
    // pour un autre sens) au cas ou d'anciens reglages sauvegardes les
    // referencent encore : NormalizeAccessibilityComfortProfile les fait
    // retomber sur "custom".
    private static readonly IReadOnlyList<AccessibilityComfortProfilePreset> AccessibilityComfortPresets =
    [
        new(
            "balanced",
            "Aucune aide",
            "Point de depart : aucune aide de confort activee (seul le focus visible reste, comme partout ailleurs dans Lumora). Choisissez un profil ci-dessous pour activer des aides.",
            HighContrast: false,
            LargeText: false,
            ReduceMotion: false,
            VisibleFocus: true,
            ReduceBlueLight: false,
            ReadAloud: false,
            ReadingLens: false,
            ReadingGuide: false),
        new(
            "vision",
            "Vision fatiguee",
            "Adoucit l'affichage pour les yeux fatigues : teinte plus chaude (moins de lumiere bleue), texte agrandi et transitions limitees. Pas de contraste force - le contraste renforce est fait pour la basse vision, pas pour la fatigue, et va dans le sens inverse de ce qu'on cherche ici.",
            HighContrast: false,
            LargeText: true,
            ReduceMotion: true,
            VisibleFocus: true,
            ReduceBlueLight: true,
            ReadAloud: false,
            ReadingLens: false,
            ReadingGuide: true),
        new(
            "rescue",
            "Mode secours",
            "Reprend la main tres vite quand une page ou l'interface devient penible: texte agrandi, contraste renforce, repères nets et aide de lecture prête.",
            HighContrast: true,
            LargeText: true,
            ReduceMotion: true,
            VisibleFocus: true,
            ReduceBlueLight: false,
            ReadAloud: false,
            ReadingLens: true,
            ReadingGuide: true)
    ];

    private static string NormalizeAccessibilityComfortProfile(string? profileKey) =>
        profileKey?.Trim().ToLowerInvariant() switch
        {
            "balanced" or "vision" or "rescue" or "custom" => profileKey.Trim().ToLowerInvariant(),
            // Toute cle qui ne correspond plus a un profil existant (y compris
            // les deux profils retires le 2026-07-20, voir plus haut) retombe
            // proprement ici - les interrupteurs individuels sauvegardes
            // restent, eux, inchanges.
            _ => "custom"
        };

    private static AccessibilityComfortProfilePreset? FindAccessibilityComfortPreset(string? profileKey) =>
        AccessibilityComfortPresets.FirstOrDefault(p =>
            string.Equals(p.Key, NormalizeAccessibilityComfortProfile(profileKey), StringComparison.OrdinalIgnoreCase));

    private void AccessibilityComfortProfileRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUiSettingsSave) return;
        if (AccessibilityComfortProfileRadio.SelectedItem is not RadioButton radio) return;

        var profileKey = NormalizeAccessibilityComfortProfile(radio.Tag?.ToString());
        if (profileKey == "custom")
        {
            if (ResolveAccessibilityComfortProfileFromControls() != "custom")
            {
                UpdateAccessibilityComfortProfileFromControls();
                StatusText.Text = "Le profil Personnalise apparait automatiquement des que vous melangez les reglages a la main.";
                return;
            }

            UpdateAccessibilityComfortProfileSummary("custom");
            StatusText.Text = "Profil personnalise : reglez chaque aide de confort a votre rythme.";
            return;
        }

        ApplyAccessibilityComfortProfile(profileKey);
    }

    private void ApplyAccessibilityComfortProfile(string profileKey)
    {
        var preset = FindAccessibilityComfortPreset(profileKey);
        if (preset is null)
        {
            UpdateAccessibilityComfortProfileFromControls();
            return;
        }

        if (string.Equals(preset.Key, "rescue", StringComparison.OrdinalIgnoreCase))
        {
            CaptureAccessibilityRescueSnapshotIfNeeded();
        }

        _suppressUiSettingsSave = true;
        try
        {
            AccessibilityHighContrastSwitch.IsOn = preset.HighContrast;
            AccessibilityLargeTextSwitch.IsOn = preset.LargeText;
            AccessibilityReduceMotionSwitch.IsOn = preset.ReduceMotion;
            AccessibilityVisibleFocusSwitch.IsOn = preset.VisibleFocus;
            AccessibilityReduceBlueLightSwitch.IsOn = preset.ReduceBlueLight;
            ReadAloudEnabledSwitch.IsOn = preset.ReadAloud;
            ReadingLensEnabledSwitch.IsOn = preset.ReadingLens;
            ReadingGuideEnabledSwitch.IsOn = preset.ReadingGuide;
            if (string.Equals(preset.Key, "rescue", StringComparison.OrdinalIgnoreCase))
            {
                SelectComboByTag(ReadingGuideBandHeightCombo, "220", "220");
            }
        }
        finally
        {
            _suppressUiSettingsSave = false;
        }

        UpdateAccessibilityComfortProfileFromControls();
        ApplyAccessibilityComfortSideEffects();

        UpdateStatusText(string.Equals(preset.Key, "rescue", StringComparison.OrdinalIgnoreCase)
            ? "Mode secours active. Ctrl+Alt+X pour revenir a l'etat d'avant."
            : $"Profil de confort active : {preset.Label}.");
    }

    // Point d'application unique pour tout changement de confort (preset,
    // restauration du mode secours...), quel que soit le chemin declencheur.
    // Avant cette extraction, ApplyAccessibilityComfortSnapshot() (retour du
    // mode secours) oubliait RefreshNovaHomePages() alors qu'ApplyAccessibilityComfortProfile()
    // l'appelait deja : meme classe de bug que celui signale le 2026-07-20
    // ("Vision fatiguee" ne rafraichissait pas la page d'accueil deja ouverte),
    // juste sur un autre chemin. Centraliser ici evite qu'un futur appelant
    // oublie l'un de ces effets.
    private void ApplyAccessibilityComfortSideEffects()
    {
        SaveUiSettings();
        ApplyAccessibilitySettings();
        RefreshNovaHomePages();
        UpdateReadAloudButtonVisibility();
        UpdateReadingLensButtonVisibility();
        if (!ReadAloudEnabledSwitch.IsOn)
        {
            _readAloudService.Stop();
        }
        _ = ApplyReadingGuideToAllTabsAsync();
        _ = ApplyAccessibilityVisionToAllTabsAsync();
    }

    private void UpdateAccessibilityComfortProfileFromControls()
    {
        var profileKey = ResolveAccessibilityComfortProfileFromControls();
        _uiSettings.AccessibilityComfortProfile = profileKey;
        var previousSuppressState = _suppressUiSettingsSave;
        _suppressUiSettingsSave = true;
        try
        {
            // "rescue" n'a plus de RadioButton dans la liste de profils (voir
            // la carte "Mode secours" dediee) : le forcer via SelectRadioByTag
            // retomberait a tort sur "balanced" (Aucune aide), ce qui
            // afficherait un profil errone pendant que le mode secours est
            // reellement actif.
            if (string.Equals(profileKey, "rescue", StringComparison.OrdinalIgnoreCase))
            {
                AccessibilityComfortProfileRadio.SelectedIndex = -1;
            }
            else
            {
                SelectRadioByTag(AccessibilityComfortProfileRadio, profileKey, "balanced");
            }
        }
        finally
        {
            _suppressUiSettingsSave = previousSuppressState;
        }

        UpdateAccessibilityComfortProfileSummary(profileKey);
        UpdateAccessibilityQuickButtonUi();
        UpdateAccessibilityQuickRescueUi();
    }

    private string ResolveAccessibilityComfortProfileFromControls()
    {
        foreach (var preset in AccessibilityComfortPresets)
        {
            if (AccessibilityHighContrastSwitch.IsOn == preset.HighContrast &&
                AccessibilityLargeTextSwitch.IsOn == preset.LargeText &&
                AccessibilityReduceMotionSwitch.IsOn == preset.ReduceMotion &&
                AccessibilityVisibleFocusSwitch.IsOn == preset.VisibleFocus &&
                AccessibilityReduceBlueLightSwitch.IsOn == preset.ReduceBlueLight &&
                ReadAloudEnabledSwitch.IsOn == preset.ReadAloud &&
                ReadingLensEnabledSwitch.IsOn == preset.ReadingLens &&
                ReadingGuideEnabledSwitch.IsOn == preset.ReadingGuide)
            {
                return preset.Key;
            }
        }

        return "custom";
    }

    private void UpdateAccessibilityComfortProfileSummary(string profileKey)
    {
        if (AccessibilityComfortProfileSummaryText is null) return;

        var normalized = NormalizeAccessibilityComfortProfile(profileKey);
        if (normalized == "rescue")
        {
            AccessibilityComfortProfileSummaryText.Text =
                "Mode secours actif : ce n'est pas un profil de cette liste, voir la carte \"Mode secours\" juste en dessous pour revenir a l'etat d'avant.";
            return;
        }

        var preset = FindAccessibilityComfortPreset(normalized);
        AccessibilityComfortProfileSummaryText.Text = normalized == "custom"
            ? "Personnalise : Lumora a detecte un melange manuel de reglages. Les interrupteurs ci-dessous gardent toujours la priorite."
            : preset?.Summary ?? "Choisissez un profil pour appliquer une base de confort locale.";
    }

    private static void SelectRadioByTag(RadioButtons radioButtons, string? tag, string fallbackTag)
    {
        radioButtons.SelectedItem = radioButtons.Items
            .OfType<RadioButton>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            ?? radioButtons.Items
                .OfType<RadioButton>()
                .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), fallbackTag, StringComparison.OrdinalIgnoreCase));

        if (radioButtons.SelectedItem is null && radioButtons.Items.Count > 0)
        {
            radioButtons.SelectedIndex = 0;
        }
    }
}
