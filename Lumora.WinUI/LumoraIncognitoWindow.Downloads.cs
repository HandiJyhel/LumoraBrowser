using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace Lumora.WinUI;

// Telechargements de fichiers (2026-08-31, retour utilisateur : "en Incognito
// on ne peut rien faire, meme pas telecharger une video"). Le fichier lui-meme
// atterrit normalement sur le disque (dossier Telechargements habituel du
// profil - meme comportement que MainWindow/LumoraAppWindow, WebView2 n'a
// jamais besoin qu'on lui precise un chemin different), exactement comme le
// navigateur Tor officiel le permet deja. Seule la LISTE ci-dessous est
// ephemere : jamais ecrite dans _historyPanel.Downloads (l'historique partage
// de la fenetre normale) - un survol de session privee qui alimenterait quand
// meme un journal permanent contredirait la promesse "session ephemere" de
// cette fenetre (voir commentaire de classe, LumoraIncognitoWindow.xaml.cs).
public sealed partial class LumoraIncognitoWindow
{
    private readonly List<DownloadEntry> _incognitoDownloads = new();

    // Meme raison/precedent que Core_DownloadStarting de LumoraAppWindow.xaml.cs :
    // args.Handled=true prend le telechargement en charge nous-memes plutot que de
    // laisser la boite de dialogue native WebView2 s'afficher (peut apparaitre
    // detachee de la fenetre, y compris sur un autre ecran).
    private void Core_DownloadStarting(CoreWebView2 sender, CoreWebView2DownloadStartingEventArgs args)
    {
        args.Handled = true;

        var entry = new DownloadEntry(args.DownloadOperation);
        entry.OnChanged += () => DispatcherQueue.TryEnqueue(RenderIncognitoDownloadsBadge);
        _incognitoDownloads.Insert(0, entry);
        RenderIncognitoDownloadsBadge();
    }

    private void RenderIncognitoDownloadsBadge()
    {
        IncognitoDownloadsButton.Visibility = Visibility.Visible;
    }

    private void IncognitoDownloadsFlyout_Opening(object sender, object e)
    {
        IncognitoDownloadsList.Children.Clear();

        if (_incognitoDownloads.Count == 0)
        {
            IncognitoDownloadsList.Children.Add(new TextBlock
            {
                Text = "Aucun téléchargement pour l'instant.",
                FontSize = 12,
                Opacity = 0.7,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)RootGrid.Resources["LumoraWindowMutedTextBrush"]
            });
            return;
        }

        foreach (var entry in _incognitoDownloads.Take(8))
        {
            var line = entry.IsCompleted
                ? entry.FileName
                : entry.IsFailed
                    ? $"{entry.FileName} — échec"
                    : $"{entry.FileName} — {entry.ProgressPercent:0}%";

            var item = new StackPanel { Spacing = 2 };
            item.Children.Add(new TextBlock
            {
                Text = line,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12.5,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)RootGrid.Resources["LumoraWindowTextBrush"]
            });

            // Meme geste que MainWindow.History.cs (BuildDownloadCard) : sans
            // ces boutons, un fichier telecharge en Incognito etait invisible
            // une fois la barre de progression disparue - aucun moyen de le
            // rouvrir depuis cette fenetre ephemere sans historique.
            if (entry.IsCompleted && File.Exists(entry.LocalPath))
            {
                var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                var openBtn = new Button { Content = "Ouvrir", Padding = new Thickness(10, 3, 10, 3), FontSize = 11.5 };
                openBtn.Click += (_, _) => OpenIncognitoDownload(entry.LocalPath);
                actions.Children.Add(openBtn);
                var folderBtn = new Button { Content = "Dossier", Padding = new Thickness(10, 3, 10, 3), FontSize = 11.5 };
                folderBtn.Click += (_, _) => OpenIncognitoDownloadFolder(entry.LocalPath);
                actions.Children.Add(folderBtn);
                item.Children.Add(actions);
            }

            IncognitoDownloadsList.Children.Add(item);
        }
    }

    private static void OpenIncognitoDownload(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch { }
    }

    private static void OpenIncognitoDownloadFolder(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
                { UseShellExecute = true });
        }
        catch { }
    }
}
