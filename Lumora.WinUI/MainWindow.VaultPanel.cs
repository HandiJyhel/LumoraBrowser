using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Lumora.WinUI.Credentials;
using Lumora.WinUI.PasswordManager;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    private VaultSortMode _vaultSortMode = VaultSortMode.Recent;
    private VaultCredential? _selectedVaultCredential;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _totpTimer;

    private async void VaultAddButton_Click(object sender, RoutedEventArgs e)
    {
        var (draft, cancelled) = await PromptNewCredentialAsync();
        if (cancelled) { StatusText.Text = "Ajout annulé."; return; }

        if (!_passwordManager.Save(draft))
        {
            StatusText.Text = "Identifiant incomplet : site et mot de passe requis.";
            return;
        }

        RefreshVaultPanel();
        StatusText.Text = $"Identifiant enregistré : {draft.Username}";
    }

    private void VaultSort_Click(object sender, RoutedEventArgs e)
    {
        _vaultSortMode = VaultSortAlphabeticalRadio.IsChecked == true
            ? VaultSortMode.Alphabetical
            : VaultSortMode.Recent;
        RefreshVaultPanel();
    }

    // Regroupe par site (sous-domaines fusionnés) via VaultGroupingService, plutôt
    // que l'ancien empilement plat carte par carte. Le détail (actions) vit dans le
    // volet de droite, alimenté par la sélection courante.
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
                    : "Aucun identifiant ne correspond à cette recherche.",
                Opacity = 0.65,
                Margin = new Thickness(0, 8, 0, 0)
            });
            ShowVaultDetail(null);
            return;
        }

        foreach (var group in VaultGroupingService.Group(credentials, _vaultSortMode))
        {
            VaultPanelItems.Children.Add(BuildVaultGroupHeader(group));
            foreach (var cred in group.Credentials)
            {
                VaultPanelItems.Children.Add(BuildVaultRow(cred));
            }
        }

        // Garde la sélection si l'identifiant existe toujours (ex. après renommage
        // ou fusion), sinon vide le volet de détail plutôt que de laisser un
        // identifiant fantôme affiché.
        var stillThere = _selectedVaultCredential is { } current
            ? credentials.FirstOrDefault(c => c.Id == current.Id)
            : null;
        ShowVaultDetail(stillThere);
    }

    private UIElement BuildVaultGroupHeader(VaultCredentialGroup group)
    {
        var row = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 6, 0, 2) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        row.Children.Add(BuildFaviconElement(group.Credentials[0].Origin, size: 16));

        var title = group.Credentials.Count > 1
            ? $"{group.DisplayName} ({group.Credentials.Count})"
            : group.DisplayName;
        var titleBlock = new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 13,
            Opacity = 0.85,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(titleBlock, 1);
        row.Children.Add(titleBlock);

        return row;
    }

    // Carte plutot que simple ligne de texte (2026-08-10, "choses a revoir" -
    // retour utilisateur sur le Coffre : "ca ressemble a un tableau Excel
    // inachevé"). Pastille de robustesse ajoutee (reutilise
    // PasswordHealthAnalyzer.EvaluateStrength, deja utilise par le Bilan de
    // sante - aucun nouvel algorithme), visible sans avoir a ouvrir le
    // detail. Bordure/coin arrondi repris de NovaChromeStrokeSoftBrush deja
    // utilise par le cadre du volet de detail juste a cote, pour rester dans
    // la meme famille visuelle.
    private UIElement BuildVaultRow(VaultCredential cred)
    {
        var isSelected = _selectedVaultCredential?.Id == cred.Id;
        var hasUser = !string.IsNullOrWhiteSpace(cred.Username);

        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 4),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = RootShell.Resources["NovaChromeStrokeSoftBrush"] as Brush,
            Background = isSelected
                ? RootShell.Resources["NovaAccentSoftBrush"] as Brush
                : new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Tag = cred
        };

        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new TextBlock
        {
            Text = hasUser ? cred.Username : "(aucun identifiant enregistré)",
            Opacity = hasUser ? 0.85 : 0.5,
            FontStyle = hasUser ? Windows.UI.Text.FontStyle.Normal : Windows.UI.Text.FontStyle.Italic,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(text, 0);
        row.Children.Add(text);

        var dot = new Ellipse { Width = 8, Height = 8, Fill = StrengthBrush(cred.Password), VerticalAlignment = VerticalAlignment.Center };
        ToolTipService.SetToolTip(dot, $"Mot de passe {StrengthLabel(cred.Password).ToLowerInvariant()}");
        Grid.SetColumn(dot, 1);
        row.Children.Add(dot);

        button.Content = row;

        button.Click += (_, _) =>
        {
            _selectedVaultCredential = cred;
            RefreshVaultPanel();
        };

        return button;
    }

    // ── Robustesse d'un mot de passe (badges) ────────────────────────────────
    // Reutilise PasswordHealthAnalyzer.EvaluateStrength (deja alimente au
    // Bilan de sante du coffre) plutot que de recalculer une notion de
    // robustesse differente ici - un seul et meme critere partout dans
    // l'app. Couleurs semantiques deja definies globalement (App.xaml),
    // jamais inventees pour l'occasion.
    private static Brush StrengthBrush(string password) => PasswordHealthAnalyzer.EvaluateStrength(password) switch
    {
        PasswordStrength.Strong => (Brush)Application.Current.Resources["NovaSuccessBrush"],
        PasswordStrength.Medium => (Brush)Application.Current.Resources["NovaWarningBrush"],
        _ => (Brush)Application.Current.Resources["NovaDangerBrush"],
    };

    private static Brush StrengthSurfaceBrush(string password) => PasswordHealthAnalyzer.EvaluateStrength(password) switch
    {
        PasswordStrength.Strong => (Brush)Application.Current.Resources["NovaSuccessSurfaceBrush"],
        PasswordStrength.Medium => (Brush)Application.Current.Resources["NovaWarningSurfaceBrush"],
        _ => (Brush)Application.Current.Resources["NovaDangerSurfaceBrush"],
    };

    private static string StrengthLabel(string password) => PasswordHealthAnalyzer.EvaluateStrength(password) switch
    {
        PasswordStrength.Strong => "Robuste",
        PasswordStrength.Medium => "Moyen",
        _ => "Faible",
    };

    private static UIElement BuildStrengthPill(string password)
    {
        var border = new Border
        {
            Background = StrengthSurfaceBrush(password),
            CornerRadius = new CornerRadius(100),
            Padding = new Thickness(9, 3, 9, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0)
        };
        border.Child = new TextBlock
        {
            Text = StrengthLabel(password),
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = StrengthBrush(password)
        };
        return border;
    }

    // Icône de site à partir du cache favicon déjà alimenté par la navigation
    // (MainWindow.Bookmarks.cs), en lecture seule : aucun téléchargement déclenché
    // depuis le coffre. Repli sur un glyphe générique si rien n'est en cache.
    private UIElement BuildFaviconElement(string origin, double size)
    {
        var path = CachedFaviconPathFor(origin);
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                return new Image
                {
                    Width = size,
                    Height = size,
                    Stretch = Stretch.Uniform,
                    Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(path))
                };
            }
            catch { /* fichier corrompu ou illisible : repli glyphe */ }
        }

        return new FontIcon
        {
            Glyph = "", // Globe
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = size * 0.85,
            Opacity = 0.55
        };
    }

    // Alimente le volet de détail (colonne de droite) pour l'identifiant
    // sélectionné. Refonte visuelle (0.93.50, retour utilisateur : "ça
    // ressemble à une pile de boutons, très moche") : une seule action mise en
    // avant (accent), "Supprimer" isolé en rouge loin de "Renommer",
    // identifiant+mot de passe regroupés dans un même bloc avec icônes de
    // copie en ligne — maquette validée en artifact avant d'écrire ce code
    // (voir MEMORY.md "Coffre V4", refonte de la fiche).
    private void ShowVaultDetail(VaultCredential? cred)
    {
        _selectedVaultCredential = cred;
        _totpTimer?.Stop();
        _totpTimer = null;
        VaultDetailPanel.Children.Clear();

        if (cred is null)
        {
            VaultDetailPanel.Visibility = Visibility.Collapsed;
            VaultDetailEmptyText.Visibility = Visibility.Visible;
            return;
        }

        VaultDetailEmptyText.Visibility = Visibility.Collapsed;
        VaultDetailPanel.Visibility = Visibility.Visible;

        var hasUser = !string.IsNullOrWhiteSpace(cred.Username);

        // ── Identité : favicon, domaine, identifiant, robustesse ────────────
        var identityRow = new Grid { ColumnSpacing = 10 };
        identityRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        identityRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        identityRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var favicon = (FrameworkElement)BuildFaviconElement(cred.Origin, size: 34);
        Grid.SetColumn(favicon, 0);
        identityRow.Children.Add(favicon);

        var titleStack = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        titleStack.Children.Add(new TextBlock
        {
            Text = PasswordManagerService.DisplayName(cred),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 18,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = hasUser ? cred.Username : "(aucun identifiant enregistré)",
            Opacity = hasUser ? 0.65 : 0.4,
            FontSize = 13,
            FontStyle = hasUser ? Windows.UI.Text.FontStyle.Normal : Windows.UI.Text.FontStyle.Italic,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        Grid.SetColumn(titleStack, 1);
        identityRow.Children.Add(titleStack);

        var strengthPill = (FrameworkElement)BuildStrengthPill(cred.Password);
        Grid.SetColumn(strengthPill, 2);
        identityRow.Children.Add(strengthPill);

        VaultDetailPanel.Children.Add(identityRow);

        // ── Identifiant + mot de passe, regroupés dans un même bloc ─────────
        var secretBox = new Border
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(0, 12, 0, 0)
        };
        var secretStack = new StackPanel();
        secretBox.Child = secretStack;

        secretStack.Children.Add(BuildSecretRow(
            "Identifiant", hasUser ? cred.Username : "(aucun)",
            (BuildGhostIconButton(new SymbolIcon(Symbol.Copy), "Copier l'identifiant", () => CopyPasswordManagerText(cred.Username, "Identifiant copié."), hasUser), null)));

        var passwordVisible = false;
        var passwordValueText = new TextBlock { FontFamily = new FontFamily("Consolas"), FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
        var revealBtn = BuildGhostIconButton(new SymbolIcon(Symbol.View), "Afficher le mot de passe", () => { });
        void RenderPasswordVisibility()
        {
            passwordValueText.Text = passwordVisible ? cred.Password : new string('•', Math.Max(cred.Password.Length, 8));
            ToolTipService.SetToolTip(revealBtn, passwordVisible ? "Masquer le mot de passe" : "Afficher le mot de passe");
            AutomationProperties.SetName(revealBtn, passwordVisible ? "Masquer le mot de passe" : "Afficher le mot de passe");
        }
        revealBtn.Click += (_, _) => { passwordVisible = !passwordVisible; RenderPasswordVisibility(); };
        RenderPasswordVisibility();
        var copyPasswordBtn = BuildGhostIconButton(new SymbolIcon(Symbol.Copy), "Copier le mot de passe", () => CopyPasswordManagerText(cred.Password, "Mot de passe copié."));
        secretStack.Children.Add(new Border { BorderThickness = new Thickness(0, 1, 0, 0), BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"] });
        secretStack.Children.Add(BuildSecretRowRaw("Mot de passe", passwordValueText, revealBtn, copyPasswordBtn));

        VaultDetailPanel.Children.Add(secretBox);

        // ── Action principale : ouvrir la page (accent), + modifier le mot de passe ──
        var primaryRow = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 12, 0, 0) };
        primaryRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        primaryRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var openBtn = new Button
        {
            Content = BuildIconTextContent(new SymbolIcon(Symbol.Forward), "Ouvrir la page de connexion"),
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        // Content composite (icône + texte) : nom accessible à poser
        // explicitement, WinUI3 ne le déduit plus tout seul (voir
        // BuildTextLinkButton pour le détail du piège).
        ApplyNovaControlAccessibility(openBtn, "Ouvrir la page de connexion");
        openBtn.Click += (_, _) => OpenVaultLoginPage(cred);
        Grid.SetColumn(openBtn, 0);
        primaryRow.Children.Add(openBtn);

        var editPasswordBtn = new Button { Content = new SymbolIcon(Symbol.Edit), Width = 44 };
        ToolTipService.SetToolTip(editPasswordBtn, "Modifier le mot de passe");
        ApplyNovaControlAccessibility(editPasswordBtn, "Modifier le mot de passe");
        editPasswordBtn.Click += async (_, _) =>
        {
            var box = new TextBox { Text = cred.Password, PlaceholderText = "Mot de passe", MinWidth = 320 };
            var dlg = new ContentDialog
            {
                Title = "Modifier le mot de passe",
                Content = box,
                PrimaryButtonText = "Enregistrer",
                CloseButtonText = "Annuler",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };
            if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
            _passwordManager.SetPasswordById(cred.Id, box.Text);
            RefreshVaultPanel();
            StatusText.Text = "Mot de passe mis à jour.";
        };
        Grid.SetColumn(editPasswordBtn, 1);
        primaryRow.Children.Add(editPasswordBtn);
        VaultDetailPanel.Children.Add(primaryRow);

        // ── Gérer : liens discrets à gauche, Supprimer isolé en rouge à droite ──
        var manageRow = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        manageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        manageRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var manageLinks = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 18 };

        var renameBtn = BuildTextLinkButton(new SymbolIcon(Symbol.Rename), "Renommer");
        renameBtn.Click += async (_, _) =>
        {
            var box = new TextBox { Text = cred.Label, PlaceholderText = "Nom personnalisé (ex. Amazon perso)", MinWidth = 320 };
            var dlg = new ContentDialog
            {
                Title = "Nom personnalisé",
                Content = box,
                PrimaryButtonText = "Enregistrer",
                CloseButtonText = "Annuler",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };
            if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
            _passwordManager.RenameById(cred.Id, box.Text);
            RefreshVaultPanel();
            StatusText.Text = "Nom mis à jour.";
        };
        manageLinks.Children.Add(renameBtn);

        var editUsernameBtn = BuildTextLinkButton(new SymbolIcon(Symbol.Contact), "Identifiant");
        editUsernameBtn.Click += async (_, _) =>
        {
            var box = new TextBox { Text = cred.Username, PlaceholderText = "Identifiant (ex. adresse email)", MinWidth = 320 };
            var dlg = new ContentDialog
            {
                Title = "Modifier l'identifiant",
                Content = box,
                PrimaryButtonText = "Enregistrer",
                CloseButtonText = "Annuler",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };
            if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
            _passwordManager.SetUsernameById(cred.Id, box.Text);
            RefreshVaultPanel();
            StatusText.Text = "Identifiant mis à jour.";
        };
        manageLinks.Children.Add(editUsernameBtn);
        Grid.SetColumn(manageLinks, 0);
        manageRow.Children.Add(manageLinks);

        var deleteBtn = BuildTextLinkButton(new SymbolIcon(Symbol.Delete), "Supprimer", (Brush)Application.Current.Resources["NovaDangerBrush"]);
        deleteBtn.Click += async (_, _) =>
        {
            var confirm = new ContentDialog
            {
                Title = "Supprimer cet identifiant ?",
                Content = $"{(!string.IsNullOrWhiteSpace(cred.Label) ? cred.Label + "\n" : "")}{cred.Origin}\n\nCette action est définitive.",
                PrimaryButtonText = "Supprimer",
                CloseButtonText = "Annuler",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
            _passwordManager.DeleteById(cred.Id);
            _selectedVaultCredential = null;
            StatusText.Text = "Identifiant supprimé.";
            RefreshVaultPanel();
        };
        Grid.SetColumn(deleteBtn, 1);
        manageRow.Children.Add(deleteBtn);
        VaultDetailPanel.Children.Add(manageRow);

        // Sections optionnelles (0.93.48) : masquées si l'utilisateur a désactivé
        // la fonction dans Paramètres > Coffre, sans jamais toucher aux données.
        if (_uiSettings.VaultTotpFeatureEnabled)
        {
            VaultDetailPanel.Children.Add(BuildTotpSection(cred));
        }
        if (_uiSettings.VaultPasskeyFeatureEnabled)
        {
            VaultDetailPanel.Children.Add(BuildPasskeySection(cred));
        }

        VaultDetailPanel.Children.Add(new TextBlock
        {
            Text = $"Mis à jour le {DateTimeOffset.FromUnixTimeSeconds(cred.UpdatedAt).LocalDateTime:dd MMMM yyyy à HH:mm}",
            Opacity = 0.45,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 14, 0, 0)
        });
    }

    // Une ligne du bloc identifiant/mot de passe : étiquette courte + valeur +
    // 1-2 boutons icône discrets (copier, afficher...), séparés par un filet
    // horizontal entre les deux lignes (posé par l'appelant).
    private static UIElement BuildSecretRow(string label, string value, (FrameworkElement action1, FrameworkElement? action2) actions) =>
        BuildSecretRowRaw(label, new TextBlock { Text = value, FontFamily = new FontFamily("Consolas"), FontSize = 14, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }, actions.action1, actions.action2);

    private static UIElement BuildSecretRowRaw(string label, FrameworkElement valueElement, FrameworkElement action1, FrameworkElement? action2 = null)
    {
        var row = new Grid { ColumnSpacing = 8, Padding = new Thickness(12, 10, 10, 10) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var labelBlock = new TextBlock
        {
            Text = label,
            FontSize = 10,
            Opacity = 0.55,
            Width = 78,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(labelBlock, 0);
        row.Children.Add(labelBlock);

        Grid.SetColumn(valueElement, 1);
        row.Children.Add(valueElement);

        Grid.SetColumn(action1, 2);
        row.Children.Add(action1);

        if (action2 is not null)
        {
            Grid.SetColumn(action2, 3);
            row.Children.Add(action2);
        }

        return row;
    }

    // Bouton icône discret (copier, afficher...) — sans fond ni bordure tant
    // qu'on ne survole pas, même esprit que les icônes des cartes du
    // Portefeuille mais sans texte à côté (les actions les plus fréquentes,
    // pas besoin de les épeler à chaque fois).
    private static Button BuildGhostIconButton(IconElement icon, string accessibleName, Action onClick, bool enabled = true)
    {
        var btn = new Button
        {
            Content = icon,
            Width = 34,
            Height = 34,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            IsEnabled = enabled
        };
        ToolTipService.SetToolTip(btn, accessibleName);
        AutomationProperties.SetName(btn, accessibleName);
        btn.Click += (_, _) => onClick();
        return btn;
    }

    // Lien texte discret (Renommer, Identifiant, Supprimer) — remplace les
    // anciens boutons pleins tous au même poids visuel ; brushColor optionnel
    // pour isoler "Supprimer" en rouge sans lui donner plus de poids qu'un lien.
    private static Button BuildTextLinkButton(IconElement icon, string text, Brush? brushColor = null)
    {
        // brushColor: null NE VEUT PAS DIRE "couleur héritée" ici - assigner
        // explicitement Foreground = null sur un Control bloque l'héritage et
        // rend le texte invisible (bug réel trouvé en vérifiant à l'écran,
        // "Renommer"/"Identifiant" totalement transparents). Toujours une
        // couleur concrète : celle fournie, sinon un gris discret par défaut.
        var color = brushColor ?? (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        icon.Foreground = color;
        content.Children.Add(icon);
        content.Children.Add(new TextBlock { Text = text, FontSize = 13, Foreground = color, VerticalAlignment = VerticalAlignment.Center });
        var button = new Button
        {
            Content = content,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4, 4, 4, 4)
        };
        // Un Button dont le Content est composite (StackPanel icône+texte, pas
        // une simple chaîne) n'a PLUS de nom accessible automatique en WinUI3
        // - contrairement à WPF, rien ne concatène le texte des enfants tout
        // seul. Bug réel trouvé en vérifiant (le lecteur d'écran ET le
        // pilotage UIA perdaient complètement ce bouton, silencieusement).
        AutomationProperties.SetName(button, text);
        return button;
    }

    // Pastille d'état (Active/Aucune) pour l'en-tête des cartes TOTP/Passkey —
    // l'œil repère l'état avant même de lire la description en dessous.
    private static Border BuildFeatureStatusChip(bool active)
    {
        var border = new Border
        {
            Background = active ? (Brush)Application.Current.Resources["NovaSuccessSurfaceBrush"] : (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            CornerRadius = new CornerRadius(100),
            Padding = new Thickness(9, 3, 9, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = active ? 1.0 : 0.7
        };
        border.Child = new TextBlock
        {
            Text = active ? "Active" : "Aucune",
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = active ? (Brush)Application.Current.Resources["NovaSuccessBrush"] : (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        };
        return border;
    }

    private static Border BuildFeatureCard(out StackPanel content)
    {
        var card = new Border
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 10, 0, 0)
        };
        content = new StackPanel { Spacing = 10 };
        card.Child = content;
        return card;
    }

    // icon accepte tout UIElement (SymbolIcon standard, ou le Path fait main
    // du cadre de scan pour la carte Passkey) - seul un IconElement a une
    // propriété Foreground à teinter, le Path du cadre de scan gère déjà sa
    // propre couleur.
    private static UIElement BuildFeatureCardHeader(UIElement icon, string title, bool active)
    {
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (icon is IconElement iconElement)
        {
            iconElement.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        }
        titleRow.Children.Add(icon);
        titleRow.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(titleRow, 0);
        row.Children.Add(titleRow);

        var chip = BuildFeatureStatusChip(active);
        Grid.SetColumn(chip, 2);
        row.Children.Add(chip);

        return row;
    }

    // Anneau de compte à rebours du code TOTP (comme Google Authenticator) —
    // remplace le texte "Expire dans Xs". updateFraction(1.0) = anneau plein
    // (code qui vient de tourner), updateFraction(0.0) = anneau vide (code sur
    // le point d'expirer). Dessiné à la main (ArcSegment) : pas de glyphe pour
    // un anneau de progression dynamique.
    private static (Canvas element, Action<double> updateFraction) BuildCountdownRing(double size, double strokeWidth)
    {
        var r = (size - strokeWidth) / 2;
        var center = size / 2;

        var canvas = new Canvas { Width = size, Height = size };

        var track = new Ellipse
        {
            Width = r * 2,
            Height = r * 2,
            Stroke = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            StrokeThickness = strokeWidth
        };
        Canvas.SetLeft(track, center - r);
        Canvas.SetTop(track, center - r);
        canvas.Children.Add(track);

        var progress = new Microsoft.UI.Xaml.Shapes.Path
        {
            Stroke = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
            StrokeThickness = strokeWidth,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        canvas.Children.Add(progress);

        void UpdateFraction(double fraction)
        {
            fraction = Math.Clamp(fraction, 0.0, 0.999);
            var angle = fraction * 2 * Math.PI;
            var start = new Point(center, center - r);
            var end = new Point(center + (r * Math.Sin(angle)), center - (r * Math.Cos(angle)));

            var figure = new PathFigure { StartPoint = start, IsClosed = false };
            figure.Segments.Add(new ArcSegment
            {
                Point = end,
                Size = new Size(r, r),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = fraction > 0.5
            });
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            progress.Data = geometry;
        }

        UpdateFraction(1.0);
        return (canvas, UpdateFraction);
    }

    // Authentification a deux facteurs (TOTP) locale, type Google Authenticator :
    // secret chiffre dans vault.lumora, code calcule sur l'appareil, rien envoye
    // nulle part.
    private UIElement BuildTotpSection(VaultCredential cred)
    {
        var hasTotp = !string.IsNullOrWhiteSpace(cred.TotpSecret);
        var card = BuildFeatureCard(out var content);
        content.Children.Add(BuildFeatureCardHeader(new SymbolIcon(Symbol.Clock), "Authentification à deux facteurs", hasTotp));

        if (!hasTotp)
        {
            var addBtn = new Button { Content = BuildIconTextContent(new SymbolIcon(Symbol.Add), "Ajouter un code TOTP") };
            ApplyNovaControlAccessibility(addBtn, "Ajouter un code TOTP");
            addBtn.Click += async (_, _) => await PromptAddTotpAsync(cred);
            content.Children.Add(addBtn);
            return card;
        }

        var (ring, updateRing) = BuildCountdownRing(46, 4);

        var codeText = new TextBlock
        {
            FontSize = 22,
            FontFamily = new FontFamily("Consolas"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        var countdownText = new TextBlock { Opacity = 0.55, FontSize = 12 };
        var codeTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        codeTextStack.Children.Add(codeText);
        codeTextStack.Children.Add(countdownText);

        void Tick()
        {
            var now = DateTimeOffset.UtcNow;
            var algorithm = TotpService.ParseAlgorithmName(cred.TotpAlgorithm);
            codeText.Text = TotpService.GenerateCode(cred.TotpSecret, now, cred.TotpDigits, cred.TotpPeriod, algorithm);
            var remaining = TotpService.SecondsRemaining(now, cred.TotpPeriod);
            countdownText.Text = $"expire dans {remaining}s";
            updateRing(remaining / (double)cred.TotpPeriod);
        }
        Tick();

        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.IsRepeating = true;
        timer.Tick += (_, _) => Tick();
        timer.Start();
        _totpTimer = timer;

        var codeRow = new Grid { ColumnSpacing = 12 };
        codeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        codeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        codeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        codeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(ring, 0);
        codeRow.Children.Add(ring);
        Grid.SetColumn(codeTextStack, 1);
        codeRow.Children.Add(codeTextStack);
        var copyCodeBtn = BuildGhostIconButton(new SymbolIcon(Symbol.Copy), "Copier le code TOTP", () => CopyPasswordManagerText(codeText.Text, "Code TOTP copié."));
        Grid.SetColumn(copyCodeBtn, 2);
        codeRow.Children.Add(copyCodeBtn);
        var removeTotpBtn = BuildGhostIconButton(new SymbolIcon(Symbol.Delete), "Supprimer le TOTP", () =>
        {
            _passwordManager.SetTotpById(cred.Id, null);
            StatusText.Text = "TOTP supprimé.";
            RefreshVaultPanel();
        });
        Grid.SetColumn(removeTotpBtn, 3);
        codeRow.Children.Add(removeTotpBtn);
        content.Children.Add(codeRow);

        return card;
    }

    // Icône "cadre de scan" (4 coins de cadrage + coche) pour l'action "Créer
    // une clé d'accès" — choisie après deux refus successifs ("clé", puis
    // "empreinte"/"visage") pour ne ressembler à AUCUNE des icônes déjà
    // utilisées ailleurs dans l'app pour un sujet voisin (glyphe clé de "Mots
    // de passe"/Coffre, glyphe clé de la tuile "Passkeys" du menu Démarrer).
    // Dessinée à la main plutôt qu'un glyphe Segoe MDL2 Assets : aucun glyphe
    // du jeu existant ne correspond à ce concept précis, et écrire un mauvais
    // code de glyphe afficherait une case vide en silence. Géométrie alignée
    // sur la maquette approuvée (viewBox SVG 0-24, mêmes coordonnées).
    private static UIElement BuildScanFrameIcon(double size = 16)
    {
        var geometry = new PathGeometry();

        PathFigure Corner(Point start, Point arcEnd, Point lineEnd)
        {
            var figure = new PathFigure { StartPoint = start, IsClosed = false };
            figure.Segments.Add(new ArcSegment { Point = arcEnd, Size = new Size(2, 2), SweepDirection = SweepDirection.Clockwise });
            figure.Segments.Add(new LineSegment { Point = lineEnd });
            return figure;
        }

        // Coin haut-gauche : (4,8) -> arc -> (6,4) -> ligne -> (8,4)
        geometry.Figures.Add(Corner(new Point(4, 8), new Point(6, 4), new Point(8, 4)));
        // Coin haut-droit : (16,4) -> ligne -> (18,4) -> arc -> (20,6) -> ligne -> (20,8)
        var topRight = new PathFigure { StartPoint = new Point(16, 4), IsClosed = false };
        topRight.Segments.Add(new LineSegment { Point = new Point(18, 4) });
        topRight.Segments.Add(new ArcSegment { Point = new Point(20, 6), Size = new Size(2, 2), SweepDirection = SweepDirection.Clockwise });
        topRight.Segments.Add(new LineSegment { Point = new Point(20, 8) });
        geometry.Figures.Add(topRight);
        // Coin bas-droit : (20,16) -> ligne -> (20,18) -> arc -> (18,20) -> ligne -> (16,20)
        var bottomRight = new PathFigure { StartPoint = new Point(20, 16), IsClosed = false };
        bottomRight.Segments.Add(new LineSegment { Point = new Point(20, 18) });
        bottomRight.Segments.Add(new ArcSegment { Point = new Point(18, 20), Size = new Size(2, 2), SweepDirection = SweepDirection.Clockwise });
        bottomRight.Segments.Add(new LineSegment { Point = new Point(16, 20) });
        geometry.Figures.Add(bottomRight);
        // Coin bas-gauche : (8,20) -> ligne -> (6,20) -> arc -> (4,18) -> ligne -> (4,16)
        var bottomLeft = new PathFigure { StartPoint = new Point(8, 20), IsClosed = false };
        bottomLeft.Segments.Add(new LineSegment { Point = new Point(6, 20) });
        bottomLeft.Segments.Add(new ArcSegment { Point = new Point(4, 18), Size = new Size(2, 2), SweepDirection = SweepDirection.Clockwise });
        bottomLeft.Segments.Add(new LineSegment { Point = new Point(4, 16) });
        geometry.Figures.Add(bottomLeft);
        // Coche centrale : (8.5,12.5) -> (10.8,14.8) -> (15.5,9.8)
        var check = new PathFigure { StartPoint = new Point(8.5, 12.5), IsClosed = false };
        check.Segments.Add(new LineSegment { Point = new Point(10.8, 14.8) });
        check.Segments.Add(new LineSegment { Point = new Point(15.5, 9.8) });
        geometry.Figures.Add(check);

        return new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = geometry,
            Stretch = Stretch.Uniform,
            Width = size,
            Height = size,
            StrokeThickness = 1.6,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Stroke = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    // Contenu icône + texte pour un Button (même convention visuelle que les
    // boutons de la barre d'outils du Coffre — Bilan, Fusionner, Importer...).
    private static UIElement BuildIconTextContent(UIElement icon, string text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(icon);
        row.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    // Clé d'accès (Passkey) du site : Lumora ne crée ni ne stocke la clé
    // WebAuthn/FIDO2 elle-même (c'est Windows Hello qui la gère), ce bloc ne
    // fait que refléter le journal local (voir MainWindow.Passkeys.cs) et
    // proposer d'aller en créer une si aucune n'existe encore pour ce site —
    // même fiche que le mot de passe et le TOTP, plus de panneau séparé
    // (0.93.46, unification Coffre V4).
    private UIElement BuildPasskeySection(VaultCredential cred)
    {
        var entry = PasskeyForOrigin(cred.Origin);
        var card = BuildFeatureCard(out var content);
        content.Children.Add(BuildFeatureCardHeader(BuildScanFrameIcon(15), "Clé d'accès", entry is not null));

        if (entry is null)
        {
            content.Children.Add(new TextBlock
            {
                Text = "Aucune clé d'accès pour ce site.",
                Opacity = 0.6,
                FontSize = 12
            });
            var createBtn = new Button { Content = BuildIconTextContent(BuildScanFrameIcon(), "Créer une clé d'accès pour ce site") };
            ApplyNovaControlAccessibility(createBtn, "Créer une clé d'accès pour ce site");
            createBtn.Click += (_, _) => OpenVaultLoginPage(cred);
            content.Children.Add(createBtn);
            return card;
        }

        content.Children.Add(new TextBlock
        {
            Text = $"Créée le {entry.CreatedAt.LocalDateTime:dd/MM/yyyy} — dernière utilisation le {entry.LastUsedAt.LocalDateTime:dd/MM/yyyy HH:mm}",
            Opacity = 0.65,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });

        var passkeyActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        var manageBtn = new Button { Content = "Gérer dans Windows" };
        ApplyNovaControlAccessibility(manageBtn, "Gérer cette clé d'accès dans les paramètres Windows");
        manageBtn.Click += async (_, _) => await OpenWindowsPasskeysSettingsAsync();
        passkeyActions.Children.Add(manageBtn);

        var forgetBtn = new Button { Content = "Oublier localement" };
        ApplyNovaControlAccessibility(forgetBtn, "Oublier cette clé d'accès localement (elle reste active dans Windows)");
        forgetBtn.Click += (_, _) =>
        {
            DeletePasskeyEntry(entry);
            StatusText.Text = "Clé d'accès retirée du suivi Lumora (elle reste active dans Windows).";
            RefreshVaultPanel();
        };
        passkeyActions.Children.Add(forgetBtn);

        content.Children.Add(passkeyActions);
        return card;
    }

    private async Task PromptAddTotpAsync(VaultCredential cred)
    {
        var box = new TextBox
        {
            PlaceholderText = "Secret TOTP ou URI otpauth://...",
            MinWidth = 380
        };
        var errorText = new TextBlock
        {
            Text = "Secret invalide. Vérifiez qu'il s'agit bien d'un secret TOTP (base32) ou d'une URI otpauth://.",
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = "Collez le secret fourni par le site (affiché généralement sous le QR code lors de l'activation), ou l'URI otpauth:// complète si vous l'avez exportée.",
            Opacity = 0.7,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(box);

        // Scan d'un QR code depuis une image (capture d'écran du QR affiché par
        // le site, ou photo transférée) : décodage 100% local (ZXing.Net), le
        // texte lu remplit simplement le champ ci-dessus, revalidé comme une
        // saisie manuelle.
        var scanBtn = new Button { Content = "Scanner un QR code (image)…" };
        var scanStatus = new TextBlock { FontSize = 12, Opacity = 0.65, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        scanBtn.Click += async (_, _) =>
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".bmp" }) picker.FileTypeFilter.Add(ext);

            var file = await picker.PickSingleFileAsync();
            if (file is null) return;

            string? decoded;
            try
            {
                decoded = await QrCodeReader.TryDecodeFileAsync(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Distinct de "aucun QR trouvé" : le fichier lui-même est inaccessible
                // (verrouillé par un autre programme, permissions) - message trompeur
                // avant ce correctif (bug réel trouvé en audit le 2026-08-19).
                scanStatus.Text = "Fichier image inaccessible (verrouillé ou permissions insuffisantes).";
                scanStatus.Visibility = Visibility.Visible;
                return;
            }

            if (decoded is null || TotpService.ParseSecretInput(decoded) is null)
            {
                scanStatus.Text = "Aucun QR code TOTP reconnu dans cette image.";
                scanStatus.Visibility = Visibility.Visible;
                return;
            }

            box.Text = decoded;
            scanStatus.Text = "QR code lu, vérifiez puis Enregistrer.";
            scanStatus.Visibility = Visibility.Visible;
            errorText.Visibility = Visibility.Collapsed;
        };
        panel.Children.Add(scanBtn);
        panel.Children.Add(scanStatus);
        panel.Children.Add(errorText);

        var dialog = new ContentDialog
        {
            Title = "Ajouter un code TOTP",
            Content = panel,
            PrimaryButtonText = "Enregistrer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (TotpService.ParseSecretInput(box.Text) is not null) return;
            args.Cancel = true;
            errorText.Visibility = Visibility.Visible;
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var account = TotpService.ParseSecretInput(box.Text);
        if (account is null) return; // garde-fou : déjà validé par PrimaryButtonClick

        _passwordManager.SetTotpById(cred.Id, account.Secret, account.Digits, account.Period, TotpService.AlgorithmName(account.Algorithm));
        StatusText.Text = "Code TOTP ajouté.";
        RefreshVaultPanel();
    }

    // Ouvre la page de connexion mémorisée (repli sur l'origine pour les anciennes
    // entrées sans login_url), puis referme le panneau au profit de la page web.
    private void OpenVaultLoginPage(VaultCredential cred)
    {
        var target = !string.IsNullOrWhiteSpace(cred.LoginUrl) ? cred.LoginUrl : cred.Origin;
        if (!BookmarkStore.IsWebUrl(target))
        {
            StatusText.Text = "Aucune page de connexion enregistrée pour cet identifiant.";
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

    // Génère un mot de passe selon les préférences persistées de l'utilisateur
    // (aléatoire configurable ou phrase de passe), partagées entre le dialogue
    // "Nouvel identifiant" et la barre de suggestion automatique.
    private string GeneratePasswordFromSettings() =>
        _uiSettings.VaultGeneratorMode == "passphrase"
            ? PasswordGenerator.GeneratePassphrase(_uiSettings.VaultGeneratorPassphraseWords)
            : PasswordGenerator.Generate(_uiSettings.VaultGeneratorLength, _uiSettings.VaultGeneratorUseSymbols);

    private async Task<(PasswordManagerEntryDraft draft, bool cancelled)> PromptNewCredentialAsync()
    {
        var labelBox    = new TextBox { PlaceholderText = "Nom optionnel (ex. Micromania perso)", MinWidth = 340 };
        var originBox   = new TextBox { PlaceholderText = "micromania.fr ou https://auth.micromania.fr", MinWidth = 340 };
        var loginUrlBox = new TextBox { PlaceholderText = "URL de connexion optionnelle", MinWidth = 340 };
        var usernameBox = new TextBox { PlaceholderText = "utilisateur@email.com" };
        var passwordBox = new PasswordBox { PlaceholderText = "Mot de passe" };

        // Générateur intégré : mode aléatoire (longueur/symboles) ou phrase de
        // passe (nombre de mots), tirage crypto-sûr. Les réglages choisis ici
        // sont mémorisés et réutilisés par la barre de suggestion automatique.
        var isPassphrase = _uiSettings.VaultGeneratorMode == "passphrase";
        var modeRandomRadio = new RadioButton { Content = "Aléatoire", GroupName = "PwGenMode", IsChecked = !isPassphrase };
        var modePassphraseRadio = new RadioButton { Content = "Phrase de passe", GroupName = "PwGenMode", IsChecked = isPassphrase };
        var modeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        modeRow.Children.Add(modeRandomRadio);
        modeRow.Children.Add(modePassphraseRadio);

        var lengthBox = new NumberBox
        {
            Value = _uiSettings.VaultGeneratorLength,
            Minimum = PasswordGenerator.MinLength,
            Maximum = 64,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Width = 100,
            Visibility = isPassphrase ? Visibility.Collapsed : Visibility.Visible
        };
        var symbolsCheck = new CheckBox
        {
            Content = "Symboles",
            IsChecked = _uiSettings.VaultGeneratorUseSymbols,
            Visibility = isPassphrase ? Visibility.Collapsed : Visibility.Visible
        };
        var wordCountBox = new NumberBox
        {
            Value = _uiSettings.VaultGeneratorPassphraseWords,
            Minimum = PasswordGenerator.MinPassphraseWords,
            Maximum = PasswordGenerator.MaxPassphraseWords,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Width = 100,
            Visibility = isPassphrase ? Visibility.Visible : Visibility.Collapsed
        };

        void UpdateGeneratorOptionsVisibility()
        {
            var passphraseMode = modePassphraseRadio.IsChecked == true;
            lengthBox.Visibility = passphraseMode ? Visibility.Collapsed : Visibility.Visible;
            symbolsCheck.Visibility = passphraseMode ? Visibility.Collapsed : Visibility.Visible;
            wordCountBox.Visibility = passphraseMode ? Visibility.Visible : Visibility.Collapsed;
        }
        modeRandomRadio.Checked += (_, _) => UpdateGeneratorOptionsVisibility();
        modePassphraseRadio.Checked += (_, _) => UpdateGeneratorOptionsVisibility();

        var generateButton = new Button { Content = "Générer" };
        generateButton.Click += (_, _) =>
        {
            _uiSettings.VaultGeneratorMode = modePassphraseRadio.IsChecked == true ? "passphrase" : "random";
            _uiSettings.VaultGeneratorLength = (int)lengthBox.Value;
            _uiSettings.VaultGeneratorUseSymbols = symbolsCheck.IsChecked == true;
            _uiSettings.VaultGeneratorPassphraseWords = (int)wordCountBox.Value;
            _uiSettings.Save(_profile.UiSettingsFile);

            passwordBox.Password = GeneratePasswordFromSettings();
            passwordBox.PasswordRevealMode = PasswordRevealMode.Visible;
        };

        var generatorOptionsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        generatorOptionsRow.Children.Add(lengthBox);
        generatorOptionsRow.Children.Add(symbolsCheck);
        generatorOptionsRow.Children.Add(wordCountBox);
        generatorOptionsRow.Children.Add(generateButton);

        var generatorRow = new StackPanel { Spacing = 6 };
        generatorRow.Children.Add(modeRow);
        generatorRow.Children.Add(generatorOptionsRow);

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
