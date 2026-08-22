using System.Collections.ObjectModel;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;
using Lumora.Privacy;
using Lumora.Privacy.NetworkBlocker;
using Lumora.Privacy.TelemetryBlocker;
using Lumora.Privacy.ParameterCleaner;
using Lumora.Privacy.HttpsEnforcer;
using Lumora.Privacy.CnameUncloaker;
using Lumora.Privacy.CosmeticFilter;
using Lumora.Privacy.ConsentManager;
using Lumora.WinUI.Credentials;
using Lumora.WinUI.PasswordManager;

namespace Lumora.WinUI;

// Fonctionnalite Mode d'usage / Compagnon Lumie. Extrait de
// MainWindow.xaml.cs (god file) : ne depend que de _uiSettings, aucun
// changement de comportement, uniquement un deplacement de code au sein
// de la meme classe partielle.
public sealed partial class MainWindow : Window
{
    private void ApplyUsageModeFromUi(string usageMode)
    {
        var previousUsageMode = _uiSettings.UsageMode;
        _uiSettings.UsageMode = usageMode;
        if (!string.Equals(previousUsageMode, usageMode, StringComparison.OrdinalIgnoreCase))
        {
            _uiSettings.LastIntroducedUsageMode = string.Empty;
        }

        ApplyUsageModePreset(usageMode);
        _uiSettings.Save(_profile.UiSettingsFile);

        ApplyUiSettings();
        RefreshNovaHomePages();
        StatusText.Text = string.Equals(usageMode, "neutral", StringComparison.OrdinalIgnoreCase)
            ? "Mode Lumora appliqué : Neutre. Accueil simplifié et modules essentiels conservés."
            : $"Mode Lumora appliqué : {UsageModeLabel(usageMode)}. Une présentation du mode est disponible sur l'accueil.";
    }

    private void UpdateUsageModeButtonUi()
    {
        var mode = (_uiSettings.UsageMode ?? "neutral").ToLowerInvariant();
        var label = UsageModeLabel(mode);
        UsageModeIcon.Glyph = mode switch
        {
            "neutral" => "\uE121",
            "focus" => "\uE8A7",
            "reading" => "\uE736",
            "creative" => "\uE70B",
            "research" => "\uE721",
            "night" => "\uE708",
            _ => "\uE9D2"
        };

        // Le contraste eleve (Confort, systeme separe - voir ComfortProfiles.cs)
        // remplace entierement la palette de couleurs du mode de navigation
        // (ApplyUsageModeChrome retourne tot si highContrast). Sans ce message,
        // rien ne signalait a l'utilisateur pourquoi changer de mode de
        // navigation n'avait plus d'effet visuel - trouve en audit le
        // 2026-07-20.
        var eclipsedByHighContrast = _uiSettings.AccessibilityHighContrast;
        UsageModeLabelText.Text = eclipsedByHighContrast ? "Mode (masqué)" : "Mode";
        UsageModeCurrentText.Text = label;

        // Menu "Mode" independant depuis le 2026-08-10 (defusion des 3 menus,
        // demande explicite utilisateur) : le tooltip ne porte plus que sur le
        // mode d'usage lui-meme, plus sur Confort (voir
        // UpdateAccessibilityMenuButtonUi, MainWindow.AccessibilityQuickActions.cs,
        // pour le menu Accessibilite desormais separe).
        var tooltip = eclipsedByHighContrast
            ? $"Mode d'usage : {label}. Couleurs remplacées tant que le contraste élevé (Confort) est actif."
            : $"Mode d'usage : {label}.";
        ToolTipService.SetToolTip(ModeUsageButton, tooltip);
        AutomationProperties.SetName(ModeUsageButton, tooltip);

        void Mark(Button button, string tag)
        {
            var active = string.Equals(mode, tag, StringComparison.OrdinalIgnoreCase);
            button.Opacity = active ? 1 : 0.78;
            button.FontWeight = active ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        }

        Mark(UsageModeNeutralButton, "neutral");
        Mark(UsageModeBalancedButton, "balanced");
        Mark(UsageModeFocusButton, "focus");
        Mark(UsageModeReadingButton, "reading");
        Mark(UsageModeCreativeButton, "creative");
        Mark(UsageModeResearchButton, "research");
        Mark(UsageModeNightButton, "night");
    }

    private void UpdateModeCompanionUi()
    {
        var mode = (_uiSettings.UsageMode ?? "neutral").ToLowerInvariant();
        var label = UsageModeLabel(mode);
        var companion = ModeCompanion(mode);

        ModeCompanionMascotIcon.Glyph = companion.Icon;
        ModeCompanionTitleText.Text = $"Compagnon {label}";
        ModeCompanionBodyText.Text = companion.Body;
        ModeCompanionMemoryTitleText.Text = companion.MemoryTitle;
        ModeCompanionMemoryBox.PlaceholderText = companion.MemoryPlaceholder;
        ModeCompanionMemoryBox.Text = CompanionMemory(mode);
        ModeCompanionPrimaryIcon.Glyph = companion.PrimaryIcon;
        ModeCompanionPrimaryTitleText.Text = companion.PrimaryTitle;
        ModeCompanionPrimaryHintText.Text = companion.PrimaryHint;
        ModeCompanionSecondaryIcon.Glyph = companion.SecondaryIcon;
        ModeCompanionSecondaryTitleText.Text = companion.SecondaryTitle;
        ModeCompanionSecondaryHintText.Text = companion.SecondaryHint;
    }

    // Factorise la resolution du compagnon du mode courant : utilisee par
    // UpdateModeCompanionUi() pour ne jamais dupliquer les textes par mode
    // (Body/MemoryPlaceholder/...).
    private ModeCompanionDefinition GetCurrentModeCompanion() =>
        ModeCompanion((_uiSettings.UsageMode ?? "neutral").ToLowerInvariant());

    // Menu "Compagnon" independant depuis le 2026-08-10 (defusion des 3 menus,
    // demande explicite utilisateur) : interrupteur simple, plus rattache a
    // l'affichage d'un mode precis ("peu importe le mode, il est toujours la").
    // CompanionOnState porte le contenu (inchange), CompanionOffState un
    // message court + le meme bouton CompanionToggleButton sert d'action
    // "Activer"/"Desactiver" selon l'etat, ecrit en toutes lettres.
    private void UpdateCompanionButtonUi()
    {
        var enabled = _uiSettings.CompanionEnabled;

        CompanionStateText.Text = enabled ? "Activé" : "Désactivé";
        CompanionAccentBar.Opacity = enabled ? 0.78 : 0.35;
        CompanionIcon.Opacity = enabled ? 0.76 : 0.4;
        ToolTipService.SetToolTip(CompanionButton, enabled ? "Compagnon : activé" : "Compagnon : désactivé");
        AutomationProperties.SetName(CompanionButton, enabled ? "Compagnon, activé" : "Compagnon, désactivé");

        CompanionOnState.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        CompanionOffState.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
        CompanionToggleButton.Content = enabled ? "Désactiver" : "Activer";

        if (enabled)
        {
            UpdateModeCompanionUi();
        }
    }

    private void CompanionFlyout_Opening(object sender, object e) => UpdateCompanionButtonUi();

    private void CompanionToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _uiSettings.CompanionEnabled = !_uiSettings.CompanionEnabled;
        _uiSettings.Save(_profile.UiSettingsFile);
        UpdateCompanionButtonUi();
        StatusText.Text = _uiSettings.CompanionEnabled ? "Compagnon Lumie activé." : "Compagnon Lumie désactivé.";
    }

    private async void ModeCompanionPrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        CompanionFlyout.Hide();
        await RunModeCompanionActionAsync(ModeCompanion((_uiSettings.UsageMode ?? "neutral").ToLowerInvariant()).PrimaryAction);
    }

    private async void ModeCompanionSecondaryButton_Click(object sender, RoutedEventArgs e)
    {
        CompanionFlyout.Hide();
        await RunModeCompanionActionAsync(ModeCompanion((_uiSettings.UsageMode ?? "neutral").ToLowerInvariant()).SecondaryAction);
    }

    private void ModeCompanionSaveButton_Click(object sender, RoutedEventArgs e) =>
        SaveCompanionMemoryAndNotify(ModeCompanionMemoryBox.Text);

    private void SaveCompanionMemoryAndNotify(string text)
    {
        var mode = (_uiSettings.UsageMode ?? "neutral").ToLowerInvariant();
        SetCompanionMemory(mode, text);
        _uiSettings.Save(_profile.UiSettingsFile);
        RefreshNovaHomePages();
        StatusText.Text = $"Lumie garde votre {ModeCompanion(mode).MemoryName}.";
    }

    private string CompanionMemory(string mode) =>
        mode switch
        {
            "focus" => _uiSettings.CompanionFocusObjective,
            "reading" => _uiSettings.CompanionReadingNote,
            "creative" => _uiSettings.CompanionCreativePostIt,
            "research" => _uiSettings.CompanionResearchTrail,
            "night" => _uiSettings.CompanionNightReminder,
            _ => _uiSettings.CompanionBalancedMemo
        };

    private void SetCompanionMemory(string mode, string value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length > 4000)
        {
            trimmed = trimmed[..4000];
        }

        switch (mode)
        {
            case "focus":
                _uiSettings.CompanionFocusObjective = trimmed;
                break;
            case "reading":
                _uiSettings.CompanionReadingNote = trimmed;
                break;
            case "creative":
                _uiSettings.CompanionCreativePostIt = trimmed;
                break;
            case "research":
                _uiSettings.CompanionResearchTrail = trimmed;
                break;
            case "night":
                _uiSettings.CompanionNightReminder = trimmed;
                break;
            default:
                _uiSettings.CompanionBalancedMemo = trimmed;
                break;
        }
    }

    private async Task RunModeCompanionActionAsync(string action)
    {
        switch (action)
        {
            case "notes":
                NotesMenu_Click(this, new RoutedEventArgs());
                StatusText.Text = "Compagnon Notes ouvert.";
                break;

            case "reader":
                ShowPanel(BrowserPanel, CurrentTab()?.Title ?? "Accueil Lumora");
                await ToggleReaderModeAsync(ensureOpen: true);
                StatusText.Text = "Compagnon Lecture ouvert.";
                break;

            case "read_aloud":
                ReadAloudFlyout.ShowAt(ReadAloudButton.Visibility == Visibility.Visible ? ReadAloudButton : CompanionButton);
                StatusText.Text = "Compagnon voix locale ouvert.";
                break;

            case "search_assist":
                await RunSearchAssistAsync(SearchAssistButton.Visibility == Visibility.Visible ? SearchAssistButton : CompanionButton);
                break;

            case "history":
                HistoryMenu_Click(this, new RoutedEventArgs());
                StatusText.Text = "Compagnon Recherche : historique local ouvert.";
                break;

            case "bookmarks":
                BookmarksMenu_Click(this, new RoutedEventArgs());
                StatusText.Text = "Compagnon Recherche : sources gardées ouvertes.";
                break;

            case "command_palette":
                if (!_uiSettings.CommandPaletteEnabled)
                {
                    StatusText.Text = "Palette Ctrl+K désactivée.";
                    return;
                }

                ShowCommandPalette();
                break;

            case "fullscreen":
                ToggleFullScreenMode();
                break;

            case "modules":
                ShowPanel(ModulesPanel, "Modules Lumora");
                StatusText.Text = "Compagnon Modules ouvert.";
                break;

            case "add_shortcut":
                await HandleNewTabShortcutMessageAsync("newtab_add_shortcut", new JsonObject());
                StatusText.Text = "Ajout d'un raccourci Lumora.";
                break;
        }
    }

    private static ModeCompanionDefinition ModeCompanion(string usageMode) =>
        usageMode switch
        {
            "neutral" => new ModeCompanionDefinition(
                "\uE121",
                "Le mode Neutre garde Lumora discret, mais laisse un accès rapide aux raccourcis et aux actions utiles.",
                "Mémo neutre",
                "votre mémo neutre",
                "Ex. rappel minimal de navigation...",
                "add_shortcut",
                "\uE710",
                "Ajouter un raccourci",
                "Garder un repère rapide même en mode neutre.",
                "command_palette",
                "\uE721",
                "Actions rapides",
                "Ouvrir Ctrl+K sans quitter la page."),
            "focus" => new ModeCompanionDefinition(
                "\uE8A7",
                "Gardez votre objectif et les commandes rapides sous la main sans revenir à l'accueil.",
                "Objectif courant",
                "votre objectif",
                "Ex. terminer cette tâche, vérifier un bug, rester sur une seule priorité...",
                "command_palette",
                "\uE721",
                "Ouvrir Ctrl+K",
                "Lancer une action sans quitter la page.",
                "fullscreen",
                "\uE740",
                "Plein écran",
                "Réduire le chrome quand la tâche demande du calme."),
            "reading" => new ModeCompanionDefinition(
                "\uE736",
                "Lecture, annotations, notes et voix locale restent accessibles pendant la navigation.",
                "Note de lecture",
                "votre note de lecture",
                "Ex. passage à relire, idée importante, page à reprendre...",
                "reader",
                "\uE736",
                "Mode lecture",
                "Lire et annoter la page active.",
                "notes",
                "\uE70B",
                "Notes de lecture",
                "Retrouver vos notes locales."),
            "creative" => new ModeCompanionDefinition(
                "\uE70B",
                "Le post-it de création devient un compagnon : vos idées restent proches pendant les pages web.",
                "Post-it de création",
                "votre post-it",
                "Ex. idée, brouillon, piste créative à ne pas perdre...",
                "notes",
                "\uE70B",
                "Post-it et notes",
                "Ouvrir le carnet local pour capturer ou reprendre une idée.",
                "search_assist",
                "\uE721",
                "Relancer l'idée",
                "Reformuler une piste avec l'assistant local si activé."),
            "research" => new ModeCompanionDefinition(
                "\uE721",
                "Collectez et comparez sans perdre le fil : historique, favoris et actions rapides restent proches.",
                "Piste de recherche",
                "votre piste",
                "Ex. source à comparer, question à vérifier, lien ou hypothèse...",
                "history",
                "\uE81C",
                "Historique local",
                "Reprendre les pistes explorées sur cet appareil.",
                "bookmarks",
                "\uE734",
                "Sources gardées",
                "Ouvrir les favoris pour classer et comparer."),
            "night" => new ModeCompanionDefinition(
                "\uE708",
                "Un compagnon plus calme pour lire, écouter ou réduire l'éclat sans chercher les commandes.",
                "Rappel calme",
                "votre rappel",
                "Ex. à reprendre demain, page à finir plus tard, action minimale...",
                "reader",
                "\uE736",
                "Lecture douce",
                "Basculer la page active en lecture.",
                "read_aloud",
                "\uE767",
                "Écoute locale",
                "Ouvrir la lecture à voix haute locale."),
            _ => new ModeCompanionDefinition(
                "\uE9D2",
                "Compagnon léger : modules et commandes utiles restent disponibles sans surcharger l'accueil.",
                "Mémo Lumora",
                "votre mémo",
                "Ex. rappel de navigation, page à consulter, petite note...",
                "modules",
                "\uE713",
                "Modules Lumora",
                "Choisir les outils visibles.",
                "command_palette",
                "\uE721",
                "Actions rapides",
                "Ouvrir la palette de commande.")
        };

    private void ApplyUsageModePreset(string? usageMode)
    {
        switch ((usageMode ?? "neutral").ToLowerInvariant())
        {
            case "neutral":
                _uiSettings.NewTabStyle = "minimal";
                _uiSettings.PersonalizationMotionStyle = "subtle";
                _uiSettings.NewTabFocusSearchOnOpen = true;
                _uiSettings.NewTabShortcutsVisible = true;
                _uiSettings.CommandPaletteEnabled = true;
                _uiSettings.CompactModeEnabled = false;
                _uiSettings.CompactModeHidesBookmarks = false;
                _uiSettings.VerticalTabsEnabled = false;
                _uiSettings.BookmarksBarVisible = true;
                _uiSettings.AddressBarSuggestionsEnabled = true;
                _uiSettings.ReadAloudEnabled = false;
                _uiSettings.SearchAssistEnabled = false;
                _uiSettings.TranslationEnabled = false;
                _uiSettings.PinnedModuleIds.Clear();
                break;

            case "focus":
                _uiSettings.NewTabStyle = "minimal";
                _uiSettings.PersonalizationMotionStyle = "subtle";
                _uiSettings.NewTabFocusSearchOnOpen = true;
                _uiSettings.CommandPaletteEnabled = true;
                _uiSettings.CompactModeEnabled = true;
                _uiSettings.CompactModeHidesBookmarks = true;
                _uiSettings.VerticalTabsEnabled = false;
                break;

            case "reading":
                _uiSettings.NewTabStyle = "calm";
                _uiSettings.PersonalizationMotionStyle = "subtle";
                _uiSettings.NewTabFocusSearchOnOpen = false;
                _uiSettings.CompactModeEnabled = false;
                _uiSettings.VerticalTabsEnabled = false;
                _uiSettings.BookmarksBarVisible = true;
                AddPinnedModule("reader");
                AddPinnedModule("notes");
                AddPinnedModule("readAloud");
                _uiSettings.ReadAloudEnabled = true;
                break;

            case "creative":
                _uiSettings.NewTabStyle = "signature";
                _uiSettings.PersonalizationMotionStyle = "dynamic";
                _uiSettings.NewTabFocusSearchOnOpen = true;
                _uiSettings.CompactModeEnabled = false;
                _uiSettings.VerticalTabsEnabled = false;
                AddPinnedModule("notes");
                AddPinnedModule("searchAssist");
                _uiSettings.SearchAssistEnabled = true;
                break;

            case "research":
                _uiSettings.NewTabStyle = "signature";
                _uiSettings.PersonalizationMotionStyle = "luminous";
                _uiSettings.NewTabFocusSearchOnOpen = true;
                _uiSettings.CompactModeEnabled = false;
                _uiSettings.VerticalTabsEnabled = true;
                _uiSettings.BookmarksBarVisible = true;
                _uiSettings.AddressBarSuggestionsEnabled = true;
                AddPinnedModule("notes");
                AddPinnedModule("searchAssist");
                AddPinnedModule("translate");
                _uiSettings.SearchAssistEnabled = true;
                break;

            case "night":
                // Ne force plus ThemeMode="dark" : ecrasait silencieusement
                // le reglage Theme choisi par l'utilisateur (Parametres >
                // Theme) des qu'il activait cette posture, sans le prevenir -
                // signale par l'utilisateur. "Nuit" reste une posture de
                // confort de lecture nocturne (compact, transparence,
                // modules de lecture), mais ne pilote plus le theme clair/
                // sombre a la place de l'utilisateur.
                _uiSettings.NewTabStyle = "calm";
                _uiSettings.PersonalizationMotionStyle = "subtle";
                _uiSettings.NewTabFocusSearchOnOpen = false;
                _uiSettings.CompactModeEnabled = true;
                _uiSettings.CompactModeHidesBookmarks = true;
                _uiSettings.VerticalTabsEnabled = false;
                _uiSettings.WindowTransparency = Math.Max(_uiSettings.WindowTransparency, 24);
                AddPinnedModule("reader");
                AddPinnedModule("readAloud");
                _uiSettings.ReadAloudEnabled = true;
                break;

            default:
                _uiSettings.NewTabStyle = "signature";
                _uiSettings.PersonalizationMotionStyle = "luminous";
                _uiSettings.NewTabFocusSearchOnOpen = false;
                _uiSettings.CommandPaletteEnabled = true;
                _uiSettings.CompactModeEnabled = false;
                _uiSettings.CompactModeHidesBookmarks = false;
                _uiSettings.VerticalTabsEnabled = false;
                _uiSettings.BookmarksBarVisible = true;
                _uiSettings.AddressBarSuggestionsEnabled = true;
                break;
        }
    }

    private void AddPinnedModule(string moduleId)
    {
        if (_uiSettings.PinnedModuleIds.Contains(moduleId, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _uiSettings.PinnedModuleIds.Add(moduleId);
    }

    private void ModulePinToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: string moduleId } toggle)
        {
            return;
        }

        var pinned = _uiSettings.PinnedModuleIds;
        if (toggle.IsChecked == true)
        {
            if (!pinned.Contains(moduleId, StringComparer.OrdinalIgnoreCase))
            {
                pinned.Add(moduleId);
            }
        }
        else
        {
            pinned.RemoveAll(id => string.Equals(id, moduleId, StringComparison.OrdinalIgnoreCase));
        }

        _uiSettings.Save(_profile.UiSettingsFile);
        UpdateModulesPinUi();
        RefreshNovaHomePages();
    }

    private void UpdateModulesPinUi()
    {
        var pinned = _uiSettings.PinnedModuleIds;
        bool IsPinned(string id) => pinned.Contains(id, StringComparer.OrdinalIgnoreCase);
        void UpdatePinToggle(ToggleButton toggle, string moduleId)
        {
            var moduleName = ModuleDisplayName(moduleId);
            var isPinned = IsPinned(moduleId);
            toggle.IsChecked = isPinned;
            var label = isPinned
                ? $"Retirer {moduleName} de la barre de modules"
                : $"Épingler {moduleName} dans la barre de modules";
            ToolTipService.SetToolTip(toggle, label);
            AutomationProperties.SetName(toggle, label);
        }

        ReaderModeButton.Visibility = IsPinned("reader") ? Visibility.Visible : Visibility.Collapsed;
        NotesModuleButton.Visibility = IsPinned("notes") ? Visibility.Visible : Visibility.Collapsed;
        ReadAloudButton.Visibility = IsPinned("readAloud") ? Visibility.Visible : Visibility.Collapsed;
        VideoDownloadButton.Visibility = IsPinned("videoDownload") ? Visibility.Visible : Visibility.Collapsed;
        SearchAssistButton.Visibility = IsPinned("searchAssist") ? Visibility.Visible : Visibility.Collapsed;
        DetachVideoPinnedButton.Visibility = IsPinned("detachVideo") ? Visibility.Visible : Visibility.Collapsed;
        TranslatePinnedButton.Visibility = IsPinned("translate") ? Visibility.Visible : Visibility.Collapsed;
        WebAppsPinnedButton.Visibility = IsPinned("webApps") ? Visibility.Visible : Visibility.Collapsed;
        DictationPinnedButton.Visibility = IsPinned("dictation") ? Visibility.Visible : Visibility.Collapsed;
        // Flux RSS et Loupe de lecture (2026-08-09) : integres au systeme de
        // pin, demande explicite - avant, boutons fixes toujours visibles.
        RssModuleButton.Visibility = IsPinned("rss") ? Visibility.Visible : Visibility.Collapsed;
        ReadingLensButton.Visibility = IsPinned("readingLens") ? Visibility.Visible : Visibility.Collapsed;

        UpdatePinToggle(PanelReaderPinToggleButton, "reader");
        UpdatePinToggle(PanelNotesPinToggleButton, "notes");
        UpdatePinToggle(PanelReadAloudPinToggleButton, "readAloud");
        UpdatePinToggle(PanelVideoDownloadPinToggleButton, "videoDownload");
        UpdatePinToggle(PanelSearchAssistPinToggleButton, "searchAssist");
        UpdatePinToggle(PanelDetachVideoPinToggleButton, "detachVideo");
        UpdatePinToggle(PanelTranslatePinToggleButton, "translate");
        UpdatePinToggle(PanelWebAppsPinToggleButton, "webApps");
        UpdatePinToggle(PanelDictationPinToggleButton, "dictation");
        UpdatePinToggle(PanelRssPinToggleButton, "rss");
        UpdatePinToggle(PanelReadingLensPinToggleButton, "readingLens");

        // Meme etat, meme fonction de mise a jour, juste un 2e endroit qui
        // l'affiche (bouton rapide "puzzle", MainWindow.xaml) - aucune 2e
        // source de verite creee.
        UpdatePinToggle(QuickReaderPinToggleButton, "reader");
        UpdatePinToggle(QuickNotesPinToggleButton, "notes");
        UpdatePinToggle(QuickReadAloudPinToggleButton, "readAloud");
        UpdatePinToggle(QuickVideoDownloadPinToggleButton, "videoDownload");
        UpdatePinToggle(QuickSearchAssistPinToggleButton, "searchAssist");
        UpdatePinToggle(QuickDetachVideoPinToggleButton, "detachVideo");
        UpdatePinToggle(QuickTranslatePinToggleButton, "translate");
        UpdatePinToggle(QuickWebAppsPinToggleButton, "webApps");
        UpdatePinToggle(QuickDictationPinToggleButton, "dictation");
        UpdatePinToggle(QuickRssPinToggleButton, "rss");
        UpdatePinToggle(QuickReadingLensPinToggleButton, "readingLens");

        // Un module qui vient d'etre epingle/desepingle doit immediatement
        // apparaitre/disparaitre de la fenetre de reorganisation SI elle est
        // deja ouverte (2026-08-14, voir MainWindow.ToolbarCustomization.cs) -
        // rien a faire si elle est fermee, pas besoin de la construire pour
        // rien a chaque bascule d'epinglage.
        if (ToolbarReorganizeOverlay.Visibility == Visibility.Visible)
        {
            RefreshToolbarReorganizeSections();
        }

        // Reevalue immediatement si le nouvel etat de pin fait deborder la
        // barre d'outils (voir CollapseOverflowingToolbarModules,
        // MainWindow.xaml.cs) - sans cet appel, epingler un module de trop
        // n'ecrasait la barre d'adresse qu'au prochain redimensionnement de
        // fenetre, pas immediatement.
        UpdateResponsiveChromeLayout();
    }

    private static string UsageModeLabel(string usageMode) =>
        usageMode switch
        {
            "neutral" => "Neutre",
            "focus" => "Focus",
            "reading" => "Lecture",
            "creative" => "Création",
            "research" => "Recherche",
            "night" => "Nuit",
            _ => "Equilibre"
        };



    private sealed record ModeCompanionDefinition(
        string Icon,
        string Body,
        string MemoryTitle,
        string MemoryName,
        string MemoryPlaceholder,
        string PrimaryAction,
        string PrimaryIcon,
        string PrimaryTitle,
        string PrimaryHint,
        string SecondaryAction,
        string SecondaryIcon,
        string SecondaryTitle,
        string SecondaryHint);
}
