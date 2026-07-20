using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Lumora.WinUI.Credentials;
using Lumora.WinUI.PasswordManager;

namespace Lumora.WinUI;

// Acces rapide aux identifiants du site courant depuis la barre d'outils, sans
// ouvrir tout le panneau coffre : meme esprit que les extensions de
// gestionnaire de mots de passe (Proton Pass, Bitwarden...). Contrairement au
// panneau complet (VaultMenu_Click), qui redemande le PIN/mot de passe a
// CHAQUE ouverture, ce popup suit simplement l'etat de verrouillage courant
// du coffre : deja deverrouille pendant la session -> acces immediat ;
// verrouille -> bouton de deverrouillage dans le popup, pas de blocage.
public sealed partial class MainWindow
{
    private void VaultQuickAccessFlyout_Opening(object sender, object e)
    {
        VaultQuickAccessPanel.Children.Clear();

        if (_isGuestMode)
        {
            VaultQuickAccessPanel.Children.Add(new TextBlock
            {
                Text = "Coffre indisponible en mode invité.",
                Opacity = 0.65,
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        var tab = CurrentTab();
        var address = tab?.View?.CoreWebView2?.Source ?? tab?.Address ?? string.Empty;
        if (!BookmarkStore.IsWebUrl(address))
        {
            VaultQuickAccessPanel.Children.Add(new TextBlock
            {
                Text = "Ouvrez une page web pour voir ses identifiants.",
                Opacity = 0.65,
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        if (_vault.IsLocked)
        {
            RenderVaultQuickAccessLocked(address);
            return;
        }

        RenderVaultQuickAccessCredentials(address);
    }

    private void RenderVaultQuickAccessLocked(string address)
    {
        VaultQuickAccessPanel.Children.Clear();
        VaultQuickAccessPanel.Children.Add(new TextBlock
        {
            Text = "Coffre verrouillé.",
            FontSize = AccessibilityBodyFontSize(),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Opacity = 0.8
        });

        var unlockBtn = new Button { Content = "Déverrouiller", FontSize = AccessibilitySecondaryFontSize() };
        ApplyNovaControlAccessibility(unlockBtn, "Deverrouiller le coffre");
        unlockBtn.Click += async (_, _) =>
        {
            if (await UnlockVaultIfNeededAsync())
            {
                RenderVaultQuickAccessCredentials(address);
            }
        };
        VaultQuickAccessPanel.Children.Add(unlockBtn);
    }

    private void RenderVaultQuickAccessCredentials(string address)
    {
        VaultQuickAccessPanel.Children.Clear();

        VaultQuickAccessPanel.Children.Add(new TextBlock
        {
            Text = "Identifiants de ce site",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = AccessibilityBodyFontSize()
        });

        var matches = _passwordManager.FindAllForAddress(address);
        if (matches.Count == 0)
        {
            VaultQuickAccessPanel.Children.Add(new TextBlock
            {
                Text = "Aucun identifiant enregistré pour ce site.",
                Opacity = 0.6,
                FontSize = AccessibilitySecondaryFontSize(),
                TextWrapping = TextWrapping.Wrap
            });
        }
        else
        {
            foreach (var cred in matches)
            {
                VaultQuickAccessPanel.Children.Add(BuildVaultQuickAccessEntry(cred));
            }
        }

        var openVaultLink = new HyperlinkButton { Content = "Ouvrir le coffre complet", FontSize = AccessibilitySecondaryFontSize(), Padding = new Thickness(0) };
        ApplyNovaControlAccessibility(openVaultLink, "Ouvrir le coffre complet");
        openVaultLink.Click += (_, _) =>
        {
            VaultQuickAccessFlyout.Hide();
            VaultMenu_Click(this, new RoutedEventArgs());
        };
        VaultQuickAccessPanel.Children.Add(openVaultLink);
    }

    private UIElement BuildVaultQuickAccessEntry(VaultCredential cred)
    {
        var hasUser = !string.IsNullOrWhiteSpace(cred.Username);
        var body = new StackPanel { Spacing = 6 };

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        headerRow.Children.Add(BuildFaviconElement(cred.Origin, size: 18));
        headerRow.Children.Add(new TextBlock
        {
            Text = hasUser ? cred.Username : PasswordManagerService.DisplayName(cred),
            FontSize = AccessibilityBodyFontSize(),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        body.Children.Add(headerRow);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        var fillBtn = new Button { Content = "Remplir", FontSize = AccessibilitySecondaryFontSize(), Padding = new Thickness(8, 4, 8, 4) };
        ApplyNovaControlAccessibility(fillBtn, $"Remplir les identifiants pour {PasswordManagerService.DisplayName(cred)}");
        fillBtn.Click += async (_, _) =>
        {
            VaultQuickAccessFlyout.Hide();
            var result = await _credentialService.FillAsync(cred);
            StatusText.Text = result.Message;
        };
        actions.Children.Add(fillBtn);

        var copyUserBtn = new Button { Content = "Copier ID", FontSize = AccessibilitySecondaryFontSize(), Padding = new Thickness(8, 4, 8, 4), IsEnabled = hasUser };
        ApplyNovaControlAccessibility(copyUserBtn, $"Copier l'identifiant pour {PasswordManagerService.DisplayName(cred)}");
        copyUserBtn.Click += (_, _) => CopyPasswordManagerText(cred.Username, "Identifiant copié.");
        actions.Children.Add(copyUserBtn);

        var copyPassBtn = new Button { Content = "Copier mdp", FontSize = AccessibilitySecondaryFontSize(), Padding = new Thickness(8, 4, 8, 4) };
        ApplyNovaControlAccessibility(copyPassBtn, $"Copier le mot de passe pour {PasswordManagerService.DisplayName(cred)}");
        copyPassBtn.Click += (_, _) => CopyPasswordManagerText(cred.Password, "Mot de passe copié.");
        actions.Children.Add(copyPassBtn);

        body.Children.Add(actions);

        // Code TOTP : instantané au moment de l'ouverture du popup, pas de
        // rafraîchissement live ici (le volet de détail du coffre complet
        // s'en charge) — suffisant pour un accès rapide "copier et coller".
        if (!string.IsNullOrWhiteSpace(cred.TotpSecret))
        {
            var totpRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var code = TotpService.GenerateCode(cred.TotpSecret, DateTimeOffset.UtcNow, cred.TotpDigits, cred.TotpPeriod);
            var codeText = new TextBlock
            {
                Text = code,
                FontFamily = new FontFamily("Consolas"),
                FontSize = AccessibilityBodyFontSize(),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            var copyTotpBtn = new Button { Content = "Copier le code", FontSize = AccessibilitySecondaryFontSize(), Padding = new Thickness(8, 4, 8, 4) };
            ApplyNovaControlAccessibility(copyTotpBtn, $"Copier le code TOTP pour {PasswordManagerService.DisplayName(cred)}");
            copyTotpBtn.Click += (_, _) => CopyPasswordManagerText(code, "Code TOTP copié.");
            totpRow.Children.Add(codeText);
            totpRow.Children.Add(copyTotpBtn);
            body.Children.Add(totpRow);
        }

        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Child = body
        };
    }
}
