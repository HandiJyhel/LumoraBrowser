using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Lumora.WinUI;

// Acces rapide a la liste des sites en connexion persistante, depuis la barre
// d'outils (bouton "Connexions", 0.93.33.0-dev) - meme esprit que
// VaultQuickAccessButton (MainWindow.VaultQuickAccess.cs) : un apercu dans un
// petit popup, avec un lien vers le panneau complet pour qui veut plus. Aucune
// logique dupliquee : la liste vient telle quelle de
// _uiSettings.TrustedSessionSites, le retrait reutilise SetTrustedSessionSite
// deja existant dans MainWindow.Sessions.cs, et le lien "Gerer toutes les
// sessions" ouvre le panneau "Sites connectes" deja existant (SessionsMenu_Click).
public sealed partial class MainWindow
{
    private void ConnectionsQuickAccessFlyout_Opening(object sender, object e) =>
        RenderConnectionsQuickAccess();

    private void RenderConnectionsQuickAccess()
    {
        ConnectionsQuickAccessPanel.Children.Clear();

        ConnectionsQuickAccessPanel.Children.Add(new TextBlock
        {
            Text = "Connexions",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = AccessibilityBodyFontSize()
        });

        ConnectionsQuickAccessPanel.Children.Add(new TextBlock
        {
            Text = _uiSettings.SessionPurgeEnabled
                ? "Effacées à chaque démarrage, sauf les sites ci-dessous."
                : "Toutes vos connexions restent actives par défaut — rien n'est effacé au démarrage.",
            Opacity = 0.7,
            FontSize = AccessibilitySecondaryFontSize(),
            TextWrapping = TextWrapping.Wrap
        });

        if (_uiSettings.TrustedSessionSites.Count == 0)
        {
            ConnectionsQuickAccessPanel.Children.Add(new TextBlock
            {
                Text = "Aucun site marqué comme exception pour l'instant.",
                Opacity = 0.6,
                FontSize = AccessibilitySecondaryFontSize(),
                TextWrapping = TextWrapping.Wrap
            });
        }
        else
        {
            foreach (var site in _uiSettings.TrustedSessionSites.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                ConnectionsQuickAccessPanel.Children.Add(BuildConnectionsQuickAccessEntry(site));
            }
        }

        var manageLink = new HyperlinkButton
        {
            Content = "Gérer toutes les sessions",
            FontSize = AccessibilitySecondaryFontSize(),
            Padding = new Thickness(0)
        };
        ApplyNovaControlAccessibility(manageLink, "Gérer toutes les sessions");
        manageLink.Click += (_, _) =>
        {
            ConnectionsQuickAccessFlyout.Hide();
            SessionsMenu_Click(this, new RoutedEventArgs());
        };
        ConnectionsQuickAccessPanel.Children.Add(manageLink);
    }

    private UIElement BuildConnectionsQuickAccessEntry(string rootDomain)
    {
        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new TextBlock
        {
            Text = rootDomain,
            FontSize = AccessibilitySecondaryFontSize(),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var removeBtn = new Button
        {
            Content = "Retirer",
            FontSize = AccessibilitySecondaryFontSize(),
            Padding = new Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Center
        };
        ApplyNovaControlAccessibility(removeBtn, $"Retirer {rootDomain} des sites en connexion persistante");
        removeBtn.Click += (_, _) =>
        {
            SetTrustedSessionSite(rootDomain, trusted: false);
            RenderConnectionsQuickAccess();
        };
        Grid.SetColumn(removeBtn, 1);
        grid.Children.Add(removeBtn);

        return grid;
    }
}
