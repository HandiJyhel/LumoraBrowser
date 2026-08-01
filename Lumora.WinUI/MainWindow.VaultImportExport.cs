using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Lumora.WinUI.Credentials;
using Lumora.WinUI.PasswordManager;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    // Point d'entree unique pour l'import de mots de passe : propose une source
    // CSV ou, si des navigateurs Chromium tiers (Chrome, Edge, Brave...) sont
    // detectes sur la machine avec des identifiants enregistres, propose de les
    // importer directement (dechiffrement local DPAPI, aucun reseau, aucune extension).
    private async Task ImportPasswordsAsync()
    {
        var sources = PasswordImportSource.Discover();
        if (sources.Count == 0)
        {
            await ImportPasswordsCsvAsync();
            return;
        }

        var choice = await PromptPasswordImportSourceAsync(sources);
        if (choice < 0)
        {
            StatusText.Text = "Import annulé.";
            return;
        }

        if (choice == 0)
        {
            await ImportPasswordsCsvAsync();
            return;
        }

        await ImportPasswordsFromBrowserAsync(sources[choice - 1]);
    }

    // Retourne -1 (annule), 0 (fichier CSV) ou l'index+1 dans `sources`.
    private async Task<int> PromptPasswordImportSourceAsync(IReadOnlyList<PasswordImportSource> sources)
    {
        var list = new ListView { SelectionMode = ListViewSelectionMode.Single, MaxHeight = 260 };
        list.Items.Add("Fichier CSV (Proton Pass, Bitwarden, 1Password, export...)");
        foreach (var source in sources) list.Items.Add(source.Label);
        list.SelectedIndex = 1;

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock
        {
            Text = "Depuis quelle source importer les mots de passe ?",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(list);
        panel.Children.Add(new TextBlock
        {
            Text = "L'import depuis un navigateur lit son magasin local et le déchiffre sur cette machine (DPAPI), sans extension ni réseau.",
            Opacity = 0.6,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });

        var dialog = new ContentDialog
        {
            Title = "Importer des mots de passe",
            Content = panel,
            PrimaryButtonText = "Continuer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return -1;
        return list.SelectedIndex < 0 ? -1 : list.SelectedIndex;
    }

    private async Task ImportPasswordsFromBrowserAsync(PasswordImportSource source)
    {
        try
        {
            var creds = source.ReadCredentials();
            if (creds.Count == 0)
            {
                StatusText.Text = $"Aucun identifiant lisible depuis {source.Browser}.";
                return;
            }

            var confirm = new ContentDialog
            {
                Title = "Importer ces identifiants ?",
                Content = $"{creds.Count} identifiant(s) détecté(s) dans {source.Browser} ({source.Profile}).\n\n" +
                          "Ils seront déchiffrés localement puis stockés dans vault.lumora.",
                PrimaryButtonText = "Importer",
                CloseButtonText = "Annuler",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary)
            {
                StatusText.Text = "Import navigateur annulé.";
                return;
            }

            var n = _passwordManager.ImportClear(creds);
            RefreshVaultPanel();
            StatusText.Text = $"Import terminé : {n} identifiant(s) ajouté(s) ou mis à jour depuis {source.Browser}.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erreur d'import navigateur : {ex.Message}";
        }
    }

    // Importe dans vault.lumora les mots de passe éventuellement présents dans le magasin
    // Chromium (legacy / migration). En mode 100% maison, Chromium ne stocke plus rien de
    // neuf, mais cette lecture récupère ce qui aurait été enregistré avant la bascule.
    private int SyncFromBrowserStore()
    {
        if (_isGuestMode || _vault.IsLocked) return 0;
        try
        {
            var creds = ChromiumCredentialReader.Read(_profile.BrowserDataDir);
            return creds.Count > 0 ? _vault.ImportClear(creds) : 0;
        }
        catch { return 0; }
    }

    // Migration 100% maison : rapatrie ce que Chromium a déjà enregistré dans vault.lumora,
    // puis EFFACE définitivement le coffre de mots de passe de Chromium, pour que le seul
    // stockage soit vault.lumora.
    private async Task MigrateAndClearBrowserPasswordsAsync(CoreWebView2? core = null)
    {
        // Garde essentielle : ne jamais vider Chromium si le coffre est verrouillé
        // (on ne pourrait pas rapatrier d'abord → perte de données).
        if (_isGuestMode || _vault.IsLocked) return;
        // Une fois par lancement suffit : le profil Chromium est partagé par tous
        // les moteurs (un WebView2 par onglet).
        if (_browserPasswordsMigrated) return;
        try
        {
            SyncFromBrowserStore();
            core ??= _browserView?.CoreWebView2;
            if (core is not null)
            {
                await core.Profile.ClearBrowsingDataAsync(
                    Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.PasswordAutosave
                    | Microsoft.Web.WebView2.Core.CoreWebView2BrowsingDataKinds.GeneralAutofill);
                _browserPasswordsMigrated = true;
            }
        }
        catch { }
    }

    private void VaultRefreshButton_Click(object sender, RoutedEventArgs e)
    {
        var n = SyncFromBrowserStore();
        RefreshVaultPanel();
        StatusText.Text = n > 0
            ? $"Coffre synchronisé : {n} identifiant(s) depuis le navigateur."
            : "Coffre à jour.";
    }

    // Fusion des doublons d'import (meme site racine, meme identifiant, meme mot
    // de passe) : detection d'abord, puis confirmation explicite avant suppression.
    private async void VaultMergeButton_Click(object sender, RoutedEventArgs e)
    {
        var duplicates = _passwordManager.FindDuplicates();
        if (duplicates.Count == 0)
        {
            StatusText.Text = "Aucun doublon dans le coffre.";
            return;
        }

        var confirm = new ContentDialog
        {
            Title = "Fusionner les doublons ?",
            Content = $"{duplicates.Count} entrée(s) en double détectée(s) : même site, même identifiant et même " +
                      "mot de passe enregistrés plusieurs fois (souvent des variantes www. ou sous-domaines " +
                      "issus d'un import).\n\nChaque groupe sera réduit à une seule entrée. Deux comptes dont " +
                      "le mot de passe diffère ne sont jamais fusionnés.",
            PrimaryButtonText = "Fusionner",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            StatusText.Text = "Fusion annulée.";
            return;
        }

        var n = _passwordManager.MergeDuplicates();
        RefreshVaultPanel();
        StatusText.Text = $"Fusion terminée : {n} doublon(s) supprimé(s).";
    }

    // ── Export / Import (maîtrise du fichier par l'utilisateur) ────────────────

    private async void VaultExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vault.IsLocked && !await UnlockVaultIfNeededAsync()) return;

        var items = _passwordManager.ExportClear();
        if (items.Count == 0) { StatusText.Text = "Coffre vide : rien à exporter."; return; }

        // Avertissement explicite : l'export produit un fichier EN CLAIR.
        var warn = new ContentDialog
        {
            Title = "Exporter en clair ?",
            Content = "Le fichier CSV généré contiendra vos mots de passe EN CLAIR, non chiffrés. " +
                      "Rangez-le en lieu sûr et supprimez-le après usage. Continuer ?",
            PrimaryButtonText = "Exporter",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };
        if (await warn.ShowAsync() != ContentDialogResult.Primary) return;

        var picker = new Windows.Storage.Pickers.FileSavePicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop,
            SuggestedFileName = "lumora-coffre-export"
        };
        picker.FileTypeChoices.Add("Fichier CSV", new List<string> { ".csv" });
        WinRT.Interop.InitializeWithWindow.Initialize(
            picker, WinRT.Interop.WindowNative.GetWindowHandle(this));

        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("name,url,username,password");
        foreach (var c in items)
            sb.AppendLine(string.Join(',',
                CredentialCsv.Escape(PasswordManagerService.DisplayName(c)),
                CredentialCsv.Escape(string.IsNullOrWhiteSpace(c.LoginUrl) ? c.Origin : c.LoginUrl),
                CredentialCsv.Escape(c.Username), CredentialCsv.Escape(c.Password)));

        await Windows.Storage.FileIO.WriteTextAsync(file, sb.ToString());
        StatusText.Text = $"Export terminé : {items.Count} identifiant(s) vers {file.Name}.";
    }

    private async void VaultImportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vault.IsLocked && !await UnlockVaultIfNeededAsync()) return;
        await ImportPasswordsAsync();
    }

    private async Task ImportPasswordsCsvAsync()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop
        };
        picker.FileTypeFilter.Add(".csv");
        WinRT.Interop.InitializeWithWindow.Initialize(
            picker, WinRT.Interop.WindowNative.GetWindowHandle(this));

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        try
        {
            var text = await Windows.Storage.FileIO.ReadTextAsync(file);
            var items = CredentialCsv.Parse(text);
            if (items.Count == 0) { StatusText.Text = "Aucun identifiant reconnu dans ce fichier."; return; }

            var confirm = new ContentDialog
            {
                Title = "Importer ce fichier CSV ?",
                Content = $"{items.Count} identifiant(s) reconnu(s) dans {file.Name}.\n\n" +
                          "Le CSV contient des mots de passe en clair. Après import, ils seront stockés dans vault.lumora.",
                PrimaryButtonText = "Importer",
                CloseButtonText = "Annuler",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary)
            {
                StatusText.Text = "Import CSV annulé.";
                return;
            }

            var n = _passwordManager.ImportClear(items);
            RefreshVaultPanel();
            StatusText.Text = $"Import terminé : {n} identifiant(s) ajouté(s) ou mis à jour.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erreur d'import : {ex.Message}";
        }
    }
}
