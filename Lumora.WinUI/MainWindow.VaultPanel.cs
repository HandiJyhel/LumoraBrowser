using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Lumora.WinUI.Credentials;
using Lumora.WinUI.PasswordManager;
using Windows.ApplicationModel.DataTransfer;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    private async void VaultAddButton_Click(object sender, RoutedEventArgs e)
    {
        var (draft, cancelled) = await PromptNewCredentialAsync();
        if (cancelled) { StatusText.Text = "Ajout annule."; return; }

        if (!_passwordManager.Save(draft))
        {
            StatusText.Text = "Identifiant incomplet : site et mot de passe requis.";
            return;
        }

        RefreshVaultPanel();
        StatusText.Text = $"Identifiant enregistre : {draft.Username}";
    }

    private void RefreshVaultPanel()
    {
        VaultPanelItems.Children.Clear();

        var query = PasswordManagerSearchBox?.Text ?? string.Empty;
        var credentials = _passwordManager.List(new PasswordManagerSearchOptions(query));
        if (credentials.Count == 0)
        {
            VaultPanelItems.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(query)
                    ? "Aucun identifiant dans le gestionnaire."
                    : "Aucun identifiant ne correspond a cette recherche.",
                Opacity = 0.65,
                Margin = new Thickness(0, 8, 0, 0)
            });
            return;
        }

        foreach (var cred in credentials)
        {
            VaultPanelItems.Children.Add(BuildVaultCard(cred));
        }
    }

    private UIElement BuildVaultCard(VaultCredential cred)
    {
        var hasLabel = !string.IsNullOrWhiteSpace(cred.Label);

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleBlock = new TextBlock
        {
            Text = PasswordManagerService.DisplayName(cred),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(titleBlock, 0);
        headerGrid.Children.Add(titleBlock);

        var openBtn = new Button
        {
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Content = new FontIcon
            {
                Glyph = "", // Globe
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14
            }
        };
        ToolTipService.SetToolTip(openBtn, "Ouvrir la page de connexion");
        openBtn.Click += (_, _) => OpenVaultLoginPage(cred);
        Grid.SetColumn(openBtn, 1);
        headerGrid.Children.Add(openBtn);

        var editBtn = new Button
        {
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        editBtn.Content = new FontIcon
        {
            Glyph = "",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 14
        };
        ToolTipService.SetToolTip(editBtn, "Renommer cet identifiant");
        editBtn.Click += async (_, _) =>
        {
            var box = new TextBox
            {
                Text = cred.Label,
                PlaceholderText = "Nom personnalise (ex. Amazon perso)",
                MinWidth = 320
            };
            var dlg = new ContentDialog
            {
                Title = "Nom personnalise",
                Content = box,
                PrimaryButtonText = "Enregistrer",
                CloseButtonText = "Annuler",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };
            if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
            _passwordManager.RenameById(cred.Id, box.Text);
            RefreshVaultPanel();
            StatusText.Text = "Nom mis a jour.";
        };
        Grid.SetColumn(editBtn, 2);
        headerGrid.Children.Add(editBtn);

        var deleteBtn = new Button
        {
            Padding = new Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Center
        };
        deleteBtn.Content = new FontIcon
        {
            Glyph = "",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 14
        };
        ToolTipService.SetToolTip(deleteBtn, "Supprimer cet identifiant");
        deleteBtn.Click += async (_, _) =>
        {
            var confirm = new ContentDialog
            {
                Title = "Supprimer cet identifiant ?",
                Content = $"{(hasLabel ? cred.Label + "\n" : "")}{cred.Origin}\n\nCette action est definitive.",
                PrimaryButtonText = "Supprimer",
                CloseButtonText = "Annuler",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
            _passwordManager.DeleteById(cred.Id);
            StatusText.Text = "Identifiant supprime.";
            RefreshVaultPanel();
        };
        Grid.SetColumn(deleteBtn, 3);
        headerGrid.Children.Add(deleteBtn);

        var body = new StackPanel();
        body.Children.Add(headerGrid);

        // Si un nom perso est défini, rappeler le site en dessous.
        if (hasLabel)
            body.Children.Add(new TextBlock
            {
                Text = cred.Origin,
                Opacity = 0.55,
                FontSize = 12,
                Margin = new Thickness(0, 2, 0, 0)
            });

        // Identifiant, ou libellé propre si vide.
        var hasUser = !string.IsNullOrWhiteSpace(cred.Username);
        body.Children.Add(new TextBlock
        {
            Text = hasUser ? cred.Username : "(aucun identifiant enregistre)",
            Opacity = hasUser ? 0.75 : 0.45,
            FontStyle = hasUser ? Windows.UI.Text.FontStyle.Normal : Windows.UI.Text.FontStyle.Italic,
            Margin = new Thickness(0, 4, 0, 0)
        });

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var copyUser = new Button
        {
            Content = "Copier identifiant",
            IsEnabled = hasUser
        };
        copyUser.Click += (_, _) => CopyPasswordManagerText(cred.Username, "Identifiant copie.");
        actions.Children.Add(copyUser);

        var copyPassword = new Button { Content = "Copier mot de passe" };
        copyPassword.Click += (_, _) => CopyPasswordManagerText(cred.Password, "Mot de passe copie.");
        actions.Children.Add(copyPassword);

        body.Children.Add(actions);

        body.Children.Add(new TextBlock
        {
            Text = $"Mis a jour : {DateTimeOffset.FromUnixTimeSeconds(cred.UpdatedAt).LocalDateTime:dd/MM/yyyy HH:mm}",
            Opacity = 0.5,
            FontSize = 12,
            Margin = new Thickness(0, 2, 0, 0)
        });

        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 10),
            Child = body
        };
    }

    // Ouvre la page de connexion mémorisée (repli sur l'origine pour les anciennes
    // entrées sans login_url), puis referme le panneau au profit de la page web.
    private void OpenVaultLoginPage(VaultCredential cred)
    {
        var target = !string.IsNullOrWhiteSpace(cred.LoginUrl) ? cred.LoginUrl : cred.Origin;
        if (!BookmarkStore.IsWebUrl(target))
        {
            StatusText.Text = "Aucune page de connexion enregistree pour cet identifiant.";
            return;
        }

        NavigateCurrentTab(target, DisplayTitle(target));
    }

    private void PasswordManagerSearchBox_TextChanged(object sender, TextChangedEventArgs e) =>
        RefreshVaultPanel();

    private void CopyPasswordManagerText(string value, string status) =>
        CopySecretToClipboard(value, status, clearAfterSeconds: 30);

    // Copie d'un secret : exclu de l'historique du presse-papiers et de la synchro
    // cloud, puis effacé après clearAfterSeconds (<= 0 = pas d'effacement auto).
    private void CopySecretToClipboard(string value, string status, int clearAfterSeconds)
    {
        var text = value ?? string.Empty;
        var package = new DataPackage();
        package.SetText(text);
        try
        {
            Clipboard.SetContentWithOptions(package, new ClipboardContentOptions
            {
                IsAllowedInHistory = false,
                IsRoamable = false
            });
        }
        catch
        {
            Clipboard.SetContent(package); // repli si l'API d'options est indisponible
        }

        StatusText.Text = status;
        if (clearAfterSeconds > 0 && text.Length > 0)
        {
            ScheduleClipboardClear(text, clearAfterSeconds);
        }
    }

    private void ScheduleClipboardClear(string copied, int seconds)
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(seconds);
        timer.IsRepeating = false;
        timer.Tick += async (t, _) =>
        {
            t.Stop();
            try
            {
                // N'effacer que si le presse-papiers contient TOUJOURS notre secret,
                // pour ne pas jeter ce que l'utilisateur aurait copié entre-temps.
                var current = Clipboard.GetContent();
                if (current.Contains(StandardDataFormats.Text) &&
                    await current.GetTextAsync() == copied)
                {
                    Clipboard.Clear();
                }
            }
            catch { }
        };
        timer.Start();
    }

    private async Task<(PasswordManagerEntryDraft draft, bool cancelled)> PromptNewCredentialAsync()
    {
        var labelBox    = new TextBox { PlaceholderText = "Nom optionnel (ex. Micromania perso)", MinWidth = 340 };
        var originBox   = new TextBox { PlaceholderText = "micromania.fr ou https://auth.micromania.fr", MinWidth = 340 };
        var loginUrlBox = new TextBox { PlaceholderText = "URL de connexion optionnelle", MinWidth = 340 };
        var usernameBox = new TextBox { PlaceholderText = "utilisateur@email.com" };
        var passwordBox = new PasswordBox { PlaceholderText = "Mot de passe" };

        // Générateur intégré : longueur réglable + tirage crypto-sûr, révélé après génération.
        var lengthBox = new NumberBox
        {
            Value = 20,
            Minimum = PasswordGenerator.MinLength,
            Maximum = 64,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Width = 120
        };
        var generateButton = new Button { Content = "Générer" };
        generateButton.Click += (_, _) =>
        {
            passwordBox.Password = PasswordGenerator.Generate((int)lengthBox.Value);
            passwordBox.PasswordRevealMode = PasswordRevealMode.Visible;
        };
        var generatorRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        generatorRow.Children.Add(lengthBox);
        generatorRow.Children.Add(generateButton);

        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new TextBlock { Text = "Nom" });
        panel.Children.Add(labelBox);
        panel.Children.Add(new TextBlock { Text = "Origine (site web)" });
        panel.Children.Add(originBox);
        panel.Children.Add(new TextBlock { Text = "Page de connexion", Margin = new Thickness(0, 8, 0, 0) });
        panel.Children.Add(loginUrlBox);
        panel.Children.Add(new TextBlock { Text = "Identifiant", Margin = new Thickness(0, 8, 0, 0) });
        panel.Children.Add(usernameBox);
        panel.Children.Add(new TextBlock { Text = "Mot de passe", Margin = new Thickness(0, 8, 0, 0) });
        panel.Children.Add(passwordBox);
        panel.Children.Add(generatorRow);

        var dialog = new ContentDialog
        {
            Title = "Nouvel identifiant",
            Content = panel,
            PrimaryButtonText = "Enregistrer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = (Content as FrameworkElement)?.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return (new PasswordManagerEntryDraft("", "", ""), true);

        return (new PasswordManagerEntryDraft(
            originBox.Text.Trim(),
            usernameBox.Text.Trim(),
            passwordBox.Password,
            loginUrlBox.Text.Trim(),
            labelBox.Text.Trim()), false);
    }
}
