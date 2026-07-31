using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Lumora.WinUI;

// ── Icône de récupération des popups en attente (0.84.0.6) ──────────────────
// Un vrai clic vers un domaine cross-site inconnu, sans motif de confiance
// (whitelist, fournisseur d'identité, même site), n'ouvre plus automatiquement
// de popup (verdict PopupVerdict.BlockPendingUserChoice) : il est indécidable
// techniquement entre popup légitime (partage, paiement) et détournement de
// clic. Au lieu de deviner, Lumora retient la popup et laisse le choix final à
// l'utilisateur via cette icône — même modèle que le blocage de popups natif
// de Chrome, Firefox et Edge.
public sealed partial class MainWindow
{
    private void RefreshPopupRecoveryIndicator()
    {
        var pendingCount = TabForView(_browserView) is { } tab
            ? _navHealth.PendingPopups(tab.Id).Count
            : 0;

        PopupRecoveryButton.Visibility = pendingCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        PopupRecoveryBadgeText.Text = pendingCount > 99 ? "99+" : pendingCount.ToString();

        // Le badge visuel ne bougeait jamais le nom accessible du bouton : un
        // lecteur d'ecran ne pouvait jamais savoir qu'une popup attendait une
        // decision sans ouvrir le panneau.
        var label = pendingCount > 0
            ? $"Popups en attente - {pendingCount}"
            : "Popups en attente";
        AutomationProperties.SetName(PopupRecoveryButton, label);
    }

    private void PopupRecoveryFlyout_Opening(object sender, object e)
    {
        PopupRecoveryPanel.Children.Clear();
        if (TabForView(_browserView) is not { } tab) return;

        foreach (var uri in _navHealth.PendingPopups(tab.Id))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock
            {
                Text = DisplayTitle(uri),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 200
            });
            var openBtn = new Button { Content = "Ouvrir", Tag = uri, Padding = new Thickness(8, 2, 8, 2) };
            openBtn.Click += PopupRecoveryOpenButton_Click;
            row.Children.Add(openBtn);
            PopupRecoveryPanel.Children.Add(row);
        }
    }

    private void PopupRecoveryOpenButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string uri }) return;
        if (TabForView(_browserView) is not { } tab) return;

        AddTab(PopupTabTitle(uri), uri, select: true);
        _navHealth.RemovePendingPopup(tab.Id, uri);
        RefreshPopupRecoveryIndicator();
        PopupRecoveryFlyout.Hide();
    }

    // « Toujours autoriser » ouvre tout ce qui est en attente sur cet onglet et
    // ajoute le site à la même liste de confiance que le bouclier confidentialité
    // (ShieldSiteExcludeToggle) : un site déjà vérifié par l'utilisateur n'a pas
    // besoin d'une seconde liste distincte pour ses popups.
    private void PopupRecoveryAlwaysAllowButton_Click(object sender, RoutedEventArgs e)
    {
        if (TabForView(_browserView) is not { } tab) return;

        var domain = _currentPageDomain;
        if (!string.IsNullOrEmpty(domain) &&
            !_uiSettings.PrivacyWhitelist.Contains(domain, StringComparer.OrdinalIgnoreCase))
        {
            _uiSettings.PrivacyWhitelist.Add(domain);
            _networkBlocker?.SetUserWhitelist(_uiSettings.PrivacyWhitelist);
            _telemetryBlocker?.SetUserWhitelist(_uiSettings.PrivacyWhitelist);
            SaveUiSettings();
            RenderPrivacyWhitelist();
        }

        foreach (var uri in _navHealth.PendingPopups(tab.Id).ToList())
        {
            AddTab(PopupTabTitle(uri), uri, select: false);
        }

        _navHealth.ClearPendingPopups(tab.Id);
        RefreshPopupRecoveryIndicator();
        PopupRecoveryFlyout.Hide();
    }
}
