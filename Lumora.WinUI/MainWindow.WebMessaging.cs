using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    // Un message web n'atteste jamais lui-même son origine de façon fiable :
    // tout champ JSON envoyé par la page (ex. un ancien "o":location.origin)
    // est falsifiable par la page elle-même via un postMessage direct, sans
    // passer par le script Lumora prévu. Les seules sources dignes de
    // confiance sont celles attestées par WebView2 : e.Source (l'URL réelle
    // du document qui a posté le message, impossible à mentir côté JS) et
    // l'adresse logique que Lumora suit lui-même pour chaque onglet
    // (TabForCore(...).Address, jamais dérivée d'une donnée web).
    private static string? OriginFromSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme is not ("http" or "https")) return null;

        var isDefaultPort =
            (uri.Scheme == "http" && uri.Port == 80) ||
            (uri.Scheme == "https" && uri.Port == 443);
        return isDefaultPort ? $"{uri.Scheme}://{uri.Host}" : $"{uri.Scheme}://{uri.Host}:{uri.Port}";
    }

    private async void BrowserCore_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = JsonNode.Parse(e.WebMessageAsJson);
            if (json is JsonValue value && value.TryGetValue<string>(out var nested))
            {
                json = JsonNode.Parse(nested);
            }

            if (json is not JsonObject obj) return;
            var type = obj["t"]?.GetValue<string>();

            // Les messages newtab_* pilotent l'UI locale de Lumora (raccourcis,
            // memoire du compagnon, ouverture de panneaux) et ne doivent jamais
            // pouvoir etre declenches par un site web quelconque qui appellerait
            // directement window.chrome.webview.postMessage : seule la page
            // d'accueil interne de Lumora (lumora://accueil, chargee via
            // NavigateToString) est legitime pour les envoyer.
            var isNewTabMessage = type is "newtab_add_shortcut" or "newtab_edit_shortcut" or "newtab_delete_shortcut"
                or "newtab_personalize" or "newtab_modules" or "newtab_mode_intro_dismiss"
                or "newtab_mode_quick_note" or "newtab_mode_action";
            if (isNewTabMessage)
            {
                var originTab = TabForCore(sender as CoreWebView2);
                if (originTab is null || !originTab.Address.Equals("lumora://accueil", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            if (type == "nova.loginDiagnostic")
            {
                HandleLoginDiagnosticMessage(e.Source, obj);
                return;
            }

            if (type == "passkey_created" || type == "passkey_used")
            {
                if (_isGuestMode) return;
                var pkOrigin = OriginFromSource(e.Source);
                if (string.IsNullOrWhiteSpace(pkOrigin)) return;
                if (type == "passkey_created")
                    RecordPasskeyCreated(pkOrigin);
                else
                    RecordPasskeyUsed(pkOrigin);
                return;
            }

            if (type == "lumora.annotation")
            {
                HandleReaderAnnotationMessage(sender as CoreWebView2, obj);
                return;
            }

            if (type == "nova.payment.form")
            {
                HandlePaymentFormDetected(sender as CoreWebView2);
                return;
            }

            if (type == "nova.fullscreenExit")
            {
                HandleContentFullScreenExitSignal(sender as CoreWebView2);
                return;
            }

            if (type == "nova.consentHandled")
            {
                HandleConsentHandledMessage(sender as CoreWebView2, e.Source, obj["method"]?.GetValue<string>());
                return;
            }

            if (type is "newtab_add_shortcut" or "newtab_edit_shortcut" or "newtab_delete_shortcut")
            {
                await HandleNewTabShortcutMessageAsync(type, obj);
                return;
            }

            if (type == "newtab_personalize")
            {
                OpenPersonalizationSettings();
                StatusText.Text = "Personnalisez votre mode, votre accueil et vos repères Lumora.";
                return;
            }

            if (type == "newtab_modules")
            {
                ShowPanel(ModulesPanel, "Modules Lumora");
                StatusText.Text = "Choisissez les modules à épingler dans la barre Lumora.";
                return;
            }

            if (type == "newtab_mode_intro_dismiss")
            {
                var mode = NormalizeUsageMode(obj["mode"]?.GetValue<string>() ?? _uiSettings.UsageMode);
                _uiSettings.LastIntroducedUsageMode = mode;
                _uiSettings.Save(_profile.UiSettingsFile);
                RefreshNovaHomePages();
                StatusText.Text = $"Présentation du mode {UsageModeLabel(mode)} masquée.";
                return;
            }

            if (type == "newtab_mode_quick_note")
            {
                HandleNewTabModeQuickNote(obj);
                return;
            }

            if (type == "newtab_mode_action")
            {
                await HandleNewTabModeActionAsync(obj["action"]?.GetValue<string>() ?? string.Empty);
                return;
            }
        }
        catch { }
    }

    private void HandleNewTabModeQuickNote(JsonObject obj)
    {
        var mode = NormalizeUsageMode(obj["mode"]?.GetValue<string>() ?? _uiSettings.UsageMode);
        var content = (obj["content"]?.GetValue<string>() ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            StatusText.Text = "La note rapide est vide.";
            return;
        }

        if (content.Length > 4000)
        {
            content = content[..4000];
        }

        SetCompanionMemory(mode, content);
        _uiSettings.Save(_profile.UiSettingsFile);
        UpdateModeCompanionUi();
        RefreshNovaHomePages();
        StatusText.Text = $"Lumie garde {NewTabModeQuickNoteTitle(mode).ToLowerInvariant()}.";
    }

    private static string NormalizeUsageMode(string? usageMode) =>
        (usageMode ?? "neutral").ToLowerInvariant() switch
        {
            "neutral" => "neutral",
            "balanced" => "balanced",
            "focus" => "focus",
            "reading" => "reading",
            "creative" => "creative",
            "research" => "research",
            "night" => "night",
            _ => "neutral"
        };

    private static string NewTabModeQuickNoteTitle(string usageMode) =>
        usageMode switch
        {
            "neutral" => "Note neutre",
            "focus" => "Objectif Focus",
            "reading" => "Note de lecture",
            "creative" => "Post-it Creation",
            "research" => "Piste de recherche",
            "night" => "Rappel Nuit",
            _ => "Note rapide Lumora"
        };

    private async Task HandleNewTabModeActionAsync(string action)
    {
        switch (action)
        {
            case "modules":
                ShowPanel(ModulesPanel, "Modules Lumora");
                StatusText.Text = "Modules Lumora ouverts.";
                break;

            case "personalize":
                OpenPersonalizationSettings();
                StatusText.Text = "Personnalisation Lumora ouverte.";
                break;

            case "add_shortcut":
                await HandleNewTabShortcutMessageAsync("newtab_add_shortcut", new JsonObject());
                break;

            case "command_palette":
                if (!_uiSettings.CommandPaletteEnabled)
                {
                    StatusText.Text = "Palette Ctrl+K désactivée. Activez-la dans Mon Lumora.";
                    return;
                }

                ShowPanel(BrowserPanel, CurrentTab()?.Title ?? "Accueil Lumora");
                ShowCommandPalette();
                break;

            case "fullscreen":
                ToggleFullScreenMode();
                break;

            case "reader":
                ShowPanel(BrowserPanel, CurrentTab()?.Title ?? "Accueil Lumora");
                await ToggleReaderModeAsync(ensureOpen: true);
                break;

            case "notes":
                NotesMenu_Click(this, new Microsoft.UI.Xaml.RoutedEventArgs());
                StatusText.Text = "Notes Lumora ouvertes.";
                break;

            case "read_aloud":
                if (!_uiSettings.ReadAloudEnabled)
                {
                    StatusText.Text = "Lecture à voix haute désactivée. Le mode Lecture ou Nuit peut l'activer.";
                    return;
                }

                ReadAloudFlyout.ShowAt(ReadAloudButton.Visibility == Microsoft.UI.Xaml.Visibility.Visible
                    ? ReadAloudButton
                    : ModulesButton);
                StatusText.Text = "Lecture à voix haute prête.";
                break;

            case "search_assist":
                if (!_uiSettings.SearchAssistEnabled)
                {
                    StatusText.Text = "Assistant IA désactivé. Le mode Création ou Recherche peut l'activer.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(AddressBox.Text))
                {
                    AddressBox.Text = "idées à explorer";
                }

                await RunSearchAssistAsync(SearchAssistButton.Visibility == Microsoft.UI.Xaml.Visibility.Visible
                    ? SearchAssistButton
                    : ModulesButton);
                break;

            case "history":
                HistoryMenu_Click(this, new Microsoft.UI.Xaml.RoutedEventArgs());
                StatusText.Text = "Historique local ouvert.";
                break;

            case "bookmarks":
                BookmarksMenu_Click(this, new Microsoft.UI.Xaml.RoutedEventArgs());
                StatusText.Text = "Favoris Lumora ouverts.";
                break;
        }
    }

    private async Task HandleNewTabShortcutMessageAsync(string type, JsonObject obj)
    {
        if (_isGuestMode)
        {
            StatusText.Text = "Raccourcis indisponibles en mode invité.";
            return;
        }

        var index = obj["i"]?.GetValue<int>() ?? -1;
        if (type == "newtab_delete_shortcut")
        {
            if (index < 0 || index >= _uiSettings.NewTabShortcuts.Count) return;
            var title = _uiSettings.NewTabShortcuts[index].Title;
            _uiSettings.NewTabShortcuts.RemoveAt(index);
            SaveNewTabShortcutSettings();
            RefreshNovaHomePages();
            StatusText.Text = $"Raccourci supprimé : {title}.";
            return;
        }

        var existingTitle = obj["title"]?.GetValue<string>() ?? string.Empty;
        var existingUrl = obj["url"]?.GetValue<string>() ?? string.Empty;
        var shortcut = await PromptNewTabShortcutAsync(
            type == "newtab_edit_shortcut" ? "Modifier le raccourci" : "Ajouter un raccourci",
            existingTitle,
            existingUrl);
        if (shortcut is null) return;

        if (type == "newtab_edit_shortcut" && index >= 0 && index < _uiSettings.NewTabShortcuts.Count)
        {
            _uiSettings.NewTabShortcuts[index] = shortcut;
            StatusText.Text = $"Raccourci modifié : {shortcut.Title}.";
        }
        else
        {
            if (_uiSettings.NewTabShortcuts.Count >= 12)
            {
                StatusText.Text = "Maximum de 12 raccourcis atteint.";
                return;
            }

            _uiSettings.NewTabShortcuts.Add(shortcut);
            // Le toggle "Afficher les raccourcis" (Parametres > Apparence) est
            // desactive par defaut sur un profil neuf. Sans cette ligne, un
            // raccourci ajoute depuis le bouton "+" de la page est enregistre
            // mais ne s'affiche jamais : l'utilisateur qui vient d'utiliser ce
            // bouton veut evidemment VOIR le raccourci qu'il vient de creer.
            _uiSettings.NewTabShortcutsVisible = true;
            StatusText.Text = $"Raccourci ajouté : {shortcut.Title}.";
        }

        SaveNewTabShortcutSettings();
        RefreshNovaHomePages();
    }

    private async Task<NewTabShortcut?> PromptNewTabShortcutAsync(string title, string currentTitle, string currentUrl)
    {
        var titleBox = new TextBox
        {
            Header = "Nom",
            Text = currentTitle,
            MaxLength = 32,
            MinWidth = 320
        };
        var urlBox = new TextBox
        {
            Header = "Adresse",
            Text = currentUrl,
            PlaceholderText = "https://exemple.com",
            MinWidth = 320
        };
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(titleBox);
        panel.Children.Add(urlBox);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = "Enregistrer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return null;

        var cleanTitle = titleBox.Text.Trim();
        var cleanUrl = NewTabMarkup.NormalizeShortcutUrl(urlBox.Text);
        if (string.IsNullOrWhiteSpace(cleanTitle) || string.IsNullOrWhiteSpace(cleanUrl))
        {
            StatusText.Text = "Nom ou adresse manquant.";
            return null;
        }

        return new NewTabShortcut(cleanTitle, cleanUrl);
    }

    private void SaveNewTabShortcutSettings()
    {
        _uiSettings.NewTabShortcuts = _uiSettings.NewTabShortcuts
            .Where(shortcut => !string.IsNullOrWhiteSpace(shortcut.Title) && !string.IsNullOrWhiteSpace(shortcut.Url))
            .Take(12)
            .ToList();
        _uiSettings.Save(_profile.UiSettingsFile);

        _suppressUiSettingsSave = true;
        try
        {
            NewTabShortcutsBox.Text = NewTabShortcutsToText(_uiSettings.NewTabShortcuts);
            NewTabShortcutsSwitch.IsOn = _uiSettings.NewTabShortcutsVisible;
        }
        finally
        {
            _suppressUiSettingsSave = false;
        }
    }

    private void RefreshNovaHomePages()
    {
        foreach (var tab in _tabs.Where(tab => tab.Address.Equals("lumora://accueil", StringComparison.OrdinalIgnoreCase)))
        {
            if (tab.View?.CoreWebView2 is not null)
            {
                tab.View.CoreWebView2.NavigateToString(HomePageHtml());
            }
        }
    }
}
