using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Lumora.WinUI;

// ── Indicateur de telechargements dans la barre d'outils (0.84.0.8) ─────────
// Depuis 0.84.0.7, Lumora supprime la boite de dialogue native de
// telechargement de WebView2/Edge (args.Handled = true dans
// CoreWebView2_DownloadStarting) : elle faisait doublon avec le panneau
// interne et pouvait s'afficher detachee sur un autre ecran. Cette icone
// redonne un signal visible pendant et apres un telechargement, comme
// Chrome/Edge/Firefox le font nativement dans leur propre barre d'outils.
public sealed partial class MainWindow
{
    private bool _hasSessionDownload;
    private int _unseenDownloadsCount;

    private void NotifyDownloadStarted()
    {
        _hasSessionDownload = true;
        _unseenDownloadsCount++;
        RefreshDownloadsIndicator();
    }

    private void RefreshDownloadsIndicator()
    {
        DownloadsIndicatorButton.Visibility = _hasSessionDownload ? Visibility.Visible : Visibility.Collapsed;
        DownloadsIndicatorBadge.Visibility = _unseenDownloadsCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        DownloadsIndicatorBadgeText.Text = _unseenDownloadsCount > 99 ? "99+" : _unseenDownloadsCount.ToString();

        // Le badge visuel ne bougeait jamais le nom accessible du bouton : un
        // lecteur d'ecran ne pouvait jamais savoir qu'un telechargement etait
        // arrive sans ouvrir le panneau.
        var label = _unseenDownloadsCount > 0
            ? $"Telechargements - {_unseenDownloadsCount} nouveau(x)"
            : "Telechargements";
        AutomationProperties.SetName(DownloadsIndicatorButton, label);
    }

    private void DownloadsIndicatorFlyout_Opening(object sender, object e)
    {
        _unseenDownloadsCount = 0;
        RefreshDownloadsIndicator();

        DownloadsIndicatorPanel.Children.Clear();
        var recent = _historyPanel.Downloads.AllEntries().Take(5).ToList();
        if (recent.Count == 0)
        {
            DownloadsIndicatorPanel.Children.Add(new TextBlock
            {
                Text = "Aucun telechargement recent.",
                Opacity = 0.65
            });
            return;
        }

        foreach (var dl in recent)
        {
            DownloadsIndicatorPanel.Children.Add(BuildDownloadCard(dl));
        }
    }

    private void DownloadsIndicatorSeeAllButton_Click(object sender, RoutedEventArgs e)
    {
        DownloadsIndicatorFlyout.Hide();
        RenderDownloads();
        ShowPanel(DownloadsPanel, "Telechargements");
    }
}
