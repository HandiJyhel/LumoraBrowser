using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
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

            if (type == "nova.loginDiagnostic")
            {
                HandleLoginDiagnosticMessage(obj);
                return;
            }

            if (type == "passkey_created" || type == "passkey_used")
            {
                if (_isGuestMode) return;
                var pkOrigin = obj["o"]?.GetValue<string>() ?? string.Empty;
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

            if (type is "newtab_add_shortcut" or "newtab_edit_shortcut" or "newtab_delete_shortcut")
            {
                await HandleNewTabShortcutMessageAsync(type, obj);
                return;
            }
        }
        catch { }
    }

    private async Task HandleNewTabShortcutMessageAsync(string type, JsonObject obj)
    {
        if (_isGuestMode)
        {
            StatusText.Text = "Raccourcis indisponibles en mode invite.";
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
            StatusText.Text = $"Raccourci supprime : {title}.";
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
            StatusText.Text = $"Raccourci modifie : {shortcut.Title}.";
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
            StatusText.Text = $"Raccourci ajoute : {shortcut.Title}.";
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
