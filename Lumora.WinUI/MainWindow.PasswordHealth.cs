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

        // Verification des fuites connues : jamais automatique (requete
        // sortante vers un service tiers), uniquement sur ce clic explicite
        // dans la boite de dialogue - voir BreachChecker pour le detail du
        // k-anonymat (seuls 5 caracteres d'un hash SHA-1 partent, jamais le
        // mot de passe ni son hash complet).
        var breachSection = new StackPanel { Spacing = 4 };
        var breachButton = new Button { Content = "Verifier aussi les fuites connues (en ligne)", Margin = new Thickness(0, 4, 0, 0) };
        var breachHint = new TextBlock
        {
            Text = "Envoie uniquement les 5 premiers caracteres du hash SHA-1 de chaque mot de passe unique (k-anonymat, service Have I Been Pwned) - jamais le mot de passe ni son hash complet.",
            Opacity = 0.6,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap
        };

        breachButton.Click += async (_, _) =>
        {
            breachButton.IsEnabled = false;
            breachButton.Content = "Verification en cours...";
            breachSection.Children.Clear();

            var uniquePasswords = credentials
                .Select(c => c.Password)
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var breachedPasswords = new HashSet<string>(StringComparer.Ordinal);
            var checkFailed = false;

            foreach (var password in uniquePasswords)
            {
                try
                {
                    if (await BreachChecker.CheckAsync(password) > 0) breachedPasswords.Add(password);
                }
                catch
                {
                    checkFailed = true;
                    break;
                }
            }

            breachButton.Visibility = Visibility.Collapsed;
            breachHint.Visibility = Visibility.Collapsed;

            if (checkFailed)
            {
                breachSection.Children.Add(new TextBlock
                {
                    Text = "Verification impossible (reseau indisponible ou service injoignable).",
                    Opacity = 0.7,
                    TextWrapping = TextWrapping.Wrap
                });
                return;
            }

            var breachedCredentials = credentials.Where(c => breachedPasswords.Contains(c.Password)).ToList();
            if (breachedCredentials.Count == 0)
            {
                breachSection.Children.Add(new TextBlock
                {
                    Text = "Aucun mot de passe trouve dans les fuites connues.",
                    TextWrapping = TextWrapping.Wrap
                });
                return;
            }

            breachSection.Children.Add(HealthSectionHeader(
                $"Trouves dans une fuite connue ({breachedCredentials.Count})",
                "Changez ces mots de passe des que possible : ils sont deja connus des attaquants."));
            foreach (var cred in breachedCredentials)
            {
                breachSection.Children.Add(HealthItem(PasswordManagerService.DisplayName(cred)));
            }
        };

        var dialog = new ContentDialog
        {
            Title = "Bilan de sante des mots de passe",
            Content = BuildHealthReportView(report, breachButton, breachHint, breachSection),
            CloseButtonText = "Fermer",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private static UIElement BuildHealthReportView(PasswordHealthReport report, UIElement breachButton, UIElement breachHint, Panel breachSection)
    {
        var panel = new StackPanel { Spacing = 12, MinWidth = 420 };

        panel.Children.Add(new TextBlock
        {
            Text = $"{report.TotalCount} identifiant(s) analyse(s), en local uniquement.",
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap
        });

        panel.Children.Add(breachButton);
        panel.Children.Add(breachHint);
        panel.Children.Add(breachSection);

        if (report.IsHealthy)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Aucun probleme local detecte : pas de mot de passe reutilise entre sites, faible ou non change depuis plus de 2 ans.",
                TextWrapping = TextWrapping.Wrap
            });
            return new ScrollViewer { Content = panel, MaxHeight = 440, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
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
