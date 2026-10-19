using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace Lumora.WinUI;

// Vue d'ensemble des Reglages : quelques phrases vraies et, quand il y a une
// action a faire, un seul bouton plein. Les textes viennent de
// SettingsOverviewText (fonction pure, testee) ; ici on ne fait que lire
// l'etat reel de l'application et afficher le resultat.
public sealed partial class MainWindow
{
    private OverviewAction _overviewAction = OverviewAction.None;

    private void RefreshSettingsOverview()
    {
        if (SettingsOverviewBody is null) return;

        DateTimeOffset? lastBackup = AccountDashboardFormatter.HasBackup(_uiSettings.LastBackupAtUnix)
            ? DateTimeOffset.FromUnixTimeSeconds(_uiSettings.LastBackupAtUnix)
            : null;
        var canBackup = !_isGuestMode && _userProfile is { HasAccountPassword: true };

        var content = SettingsOverviewText.Compose(
            _isGuestMode,
            LumoraTheme.ResolveIsDarkTheme(_uiSettings),
            CurrentAccentName(),
            _privacy.GlobalCounters.Total,
            lastBackup,
            canBackup,
            DateTimeOffset.Now);

        SettingsOverviewBody.Inlines.Clear();
        foreach (var segment in content.Segments)
        {
            if (segment.Link == OverviewLink.None)
            {
                SettingsOverviewBody.Inlines.Add(new Run { Text = segment.Text });
                continue;
            }

            var link = new Hyperlink();
            link.Inlines.Add(new Run { Text = segment.Text });
            var section = segment.Link switch
            {
                OverviewLink.Appearance => "appearance",
                OverviewLink.Privacy => "privacy",
                _ => "storage"
            };
            link.Click += (_, _) => GoToSettingsSection(section);
            SettingsOverviewBody.Inlines.Add(link);
        }

        _overviewAction = content.Action;
        var label = content.Action switch
        {
            OverviewAction.ExportBackup => "Exporter une sauvegarde",
            OverviewAction.CreateProfile => "Créer un profil",
            _ => string.Empty
        };

        SettingsOverviewPrimaryButton.Content = label;
        SettingsOverviewLinkButton.Content = label;
        SettingsOverviewPrimaryButton.Visibility =
            content.Action != OverviewAction.None && content.ActionIsPrimary ? Visibility.Visible : Visibility.Collapsed;
        SettingsOverviewLinkButton.Visibility =
            content.Action != OverviewAction.None && !content.ActionIsPrimary ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SettingsOverviewAction_Click(object sender, RoutedEventArgs e)
    {
        switch (_overviewAction)
        {
            case OverviewAction.ExportBackup:
                ExportBackupButton_Click(sender, e);
                break;
            case OverviewAction.CreateProfile:
                ShowCreateProfileOverlay();
                break;
        }
    }

    // Meme table que SettingsNavigateButton_Click, appelable depuis du code.
    private void GoToSettingsSection(string section)
    {
        var target = section switch
        {
            "appearance" => SettingsNavAppearance,
            "privacy" => SettingsNavPrivacy,
            "storage" => SettingsNavStorage,
            _ => SettingsNavOverview
        };

        target.IsChecked = true;
        SettingsNav_Click(target, new RoutedEventArgs());
    }

    // Nom des 7 pastilles du nuancier (Reglages > Mon Lumora > Couleur
    // d'accentuation). Vide = teinte Lumora d'origine.
    private string CurrentAccentName() =>
        (_uiSettings.AccentColor ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "#5B7FB5" => "Bleu",
            "#C1594F" => "Rouge",
            "#C97C3D" => "Orange",
            "#C9A63D" => "Jaune",
            "#5E9169" => "Vert",
            "#7C6BAE" => "Violet",
            "#D65129" => "Braise",
            _ => "d'origine"
        };
}
