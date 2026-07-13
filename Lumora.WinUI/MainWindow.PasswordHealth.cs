using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Lumora.WinUI.PasswordManager;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    // ── Bilan de santé des mots de passe ─────────────────────────────────────
    // Analyse 100% locale du coffre (réutilisés, faibles, anciens) : les mots de
    // passe ne quittent jamais la machine et ne sont jamais affichés dans le
    // rapport — seuls les comptes concernés sont nommés.

    private async void VaultHealthButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isGuestMode || _vault.IsLocked)
        {
            StatusText.Text = "Bilan indisponible : coffre verrouille ou mode invite.";
            return;
        }

        var credentials = _passwordManager.List();
        if (credentials.Count == 0)
        {
            StatusText.Text = "Aucun identifiant dans le coffre : rien a analyser.";
            return;
        }

        var report = PasswordHealthAnalyzer.Analyze(credentials);

        var dialog = new ContentDialog
        {
            Title = "Bilan de sante des mots de passe",
            Content = BuildHealthReportView(report),
            CloseButtonText = "Fermer",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private static UIElement BuildHealthReportView(PasswordHealthReport report)
    {
        var panel = new StackPanel { Spacing = 12, MinWidth = 420 };

        panel.Children.Add(new TextBlock
        {
            Text = $"{report.TotalCount} identifiant(s) analyse(s), en local uniquement.",
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap
        });

        if (report.IsHealthy)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Aucun probleme detecte : pas de mot de passe reutilise entre sites, faible ou non change depuis plus de 2 ans.",
                TextWrapping = TextWrapping.Wrap
            });
            return panel;
        }

        if (report.ReusedGroups.Count > 0)
        {
            panel.Children.Add(HealthSectionHeader(
                $"Reutilises sur plusieurs sites ({report.ReusedCount} compte(s))",
                "Un mot de passe vole sur un site ouvre tous les autres. Donnez un mot de passe unique a chaque site."));
            foreach (var group in report.ReusedGroups)
            {
                var names = group.Select(PasswordManagerService.DisplayName)
                                 .Distinct(StringComparer.CurrentCultureIgnoreCase);
                panel.Children.Add(HealthItem($"Meme mot de passe : {string.Join(", ", names)}"));
            }
        }

        if (report.WeakCredentials.Count > 0)
        {
            panel.Children.Add(HealthSectionHeader(
                $"Faibles ({report.WeakCredentials.Count})",
                "Trop court, trop simple ou trop courant. Le generateur de Lumora peut en proposer un solide."));
            foreach (var cred in report.WeakCredentials)
            {
                panel.Children.Add(HealthItem(PasswordManagerService.DisplayName(cred)));
            }
        }

        if (report.StaleCredentials.Count > 0)
        {
            panel.Children.Add(HealthSectionHeader(
                $"Non changes depuis plus de 2 ans ({report.StaleCredentials.Count})",
                "Pas forcement dangereux, mais un changement de temps en temps limite les degats d'une fuite passee."));
            foreach (var cred in report.StaleCredentials)
            {
                var updated = cred.UpdatedAt > 0 ? cred.UpdatedAt : cred.CreatedAt;
                var since = DateTimeOffset.FromUnixTimeSeconds(updated).ToLocalTime().ToString("d MMMM yyyy");
                panel.Children.Add(HealthItem($"{PasswordManagerService.DisplayName(cred)} — depuis le {since}"));
            }
        }

        return new ScrollViewer
        {
            Content = panel,
            MaxHeight = 440,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private static UIElement HealthSectionHeader(string title, string hint)
    {
        var header = new StackPanel { Spacing = 2, Margin = new Thickness(0, 6, 0, 0) };
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        header.Children.Add(new TextBlock
        {
            Text = hint,
            Opacity = 0.65,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });
        return header;
    }

    private static UIElement HealthItem(string text) => new TextBlock
    {
        Text = $"• {text}",
        Margin = new Thickness(8, 0, 0, 0),
        TextWrapping = TextWrapping.Wrap
    };
}
