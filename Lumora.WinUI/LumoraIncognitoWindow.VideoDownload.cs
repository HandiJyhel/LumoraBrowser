using System.Diagnostics;
using System.Text.Json.Nodes;
using Lumora.WinUI.VideoDownload;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Lumora.WinUI;

// Telechargement video YouTube (2026-08-31, retour utilisateur : "en
// Incognito on ne peut rien faire, meme pas telecharger une video"). Meme
// moteur local (yt-dlp) et memes regles que MainWindow.VideoDownload.cs -
// deliberement DUPLIQUE plutot que partage entre les deux fenetres, meme
// principe deja adopte pour le reste du chrome Incognito (voir
// IncognitoRaisedButtonTemplate, LumoraIncognitoWindow.xaml : "copie locale...
// plutot que de refactorer le partage de ressources entre fenetres") - la
// fenetre Incognito tourne dans un PROCESS separe (voir commentaire de classe)
// et ne doit rien partager avec l'etat de MainWindow. Seule difference de
// fond : la progression reste dans CETTE fenetre uniquement, jamais dans
// l'historique partage (_historyPanel.Downloads) - meme raison que
// LumoraIncognitoWindow.Downloads.cs pour les telechargements generiques.
public sealed partial class LumoraIncognitoWindow
{
    private sealed record IncognitoVideoDownloadCandidate(
        string Title,
        string Url,
        bool IsYouTube,
        string YouTubeId);

    private IncognitoVideoDownloadCandidate? _incognitoVideoCandidate;
    private bool _incognitoVideoDownloadInProgress;
    private bool _incognitoVideoEngineInstallInProgress;
    private VideoQuality _incognitoVideoQuality = VideoQuality.Best;

    private async void IncognitoVideoDownloadFlyout_Opening(object sender, object e)
    {
        IncognitoVideoDownloadStartButton.IsEnabled = false;
        IncognitoVideoDownloadQualityCombo.IsEnabled = false;
        IncognitoVideoDownloadInstallEngineButton.Visibility = Visibility.Collapsed;
        IncognitoVideoDownloadTitleText.Text = "Analyse de la page...";
        IncognitoVideoDownloadStatusText.Text = string.Empty;
        _incognitoVideoCandidate = null;

        var candidate = await DetectIncognitoVideoCandidateAsync();
        _incognitoVideoCandidate = candidate;
        RefreshIncognitoVideoEngineState();

        if (candidate is null)
        {
            IncognitoVideoDownloadTitleText.Text = "Aucune vidéo YouTube détectée sur cet onglet.";
            IncognitoVideoDownloadStatusText.Text = "Ouvre une page vidéo YouTube, puis relance ce module.";
            return;
        }

        IncognitoVideoDownloadTitleText.Text = candidate.Title;
        if (!candidate.IsYouTube)
        {
            IncognitoVideoDownloadStatusText.Text = "Cette première version du module cible les pages vidéo YouTube.";
            return;
        }

        var enginePath = YtDlpEngineProvider.FindLocalEngine();
        IncognitoVideoDownloadStartButton.IsEnabled = enginePath is not null && !_incognitoVideoDownloadInProgress;
        IncognitoVideoDownloadQualityCombo.IsEnabled = enginePath is not null && !_incognitoVideoDownloadInProgress;
        IncognitoVideoDownloadStatusText.Text = enginePath is null
            ? "Installe le moteur pour activer le téléchargement de cette vidéo."
            : FfmpegLocator.FindLocalFfmpeg() is null
                ? "Prêt à télécharger (qualité limitée : ffmpeg absent pour fusionner les flux au-delà de 720p)."
                : "Prêt à télécharger cette vidéo YouTube dans la qualité choisie ci-dessous.";
    }

    private void IncognitoVideoDownloadQualityCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IncognitoVideoDownloadQualityCombo.SelectedItem is ComboBoxItem item
            && item.Tag is string tag
            && Enum.TryParse<VideoQuality>(tag, out var quality))
        {
            _incognitoVideoQuality = quality;
        }
    }

    private void RefreshIncognitoVideoEngineState()
    {
        var enginePath = YtDlpEngineProvider.FindLocalEngine();
        IncognitoVideoDownloadEngineText.Text = enginePath is null
            ? "Moteur YouTube local introuvable."
            : $"Moteur local détecté : {Path.GetFileName(enginePath)}";

        IncognitoVideoDownloadInstallEngineButton.Visibility = enginePath is null
            ? Visibility.Visible
            : Visibility.Collapsed;
        IncognitoVideoDownloadInstallEngineButton.IsEnabled = !_incognitoVideoEngineInstallInProgress;
    }

    private async void IncognitoVideoDownloadStartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_incognitoVideoDownloadInProgress)
        {
            IncognitoVideoDownloadStatusText.Text = "Un téléchargement vidéo est déjà en cours.";
            return;
        }

        var candidate = _incognitoVideoCandidate;
        if (candidate is null || !candidate.IsYouTube)
        {
            IncognitoVideoDownloadStatusText.Text = "Aucune vidéo YouTube active.";
            return;
        }

        var enginePath = YtDlpEngineProvider.FindLocalEngine();
        if (enginePath is null)
        {
            IncognitoVideoDownloadStartButton.IsEnabled = false;
            IncognitoVideoDownloadStatusText.Text = "Moteur local introuvable : installe-le d'abord avec le bouton ci-dessus.";
            RefreshIncognitoVideoEngineState();
            return;
        }

        await StartIncognitoYouTubeDownloadAsync(candidate, enginePath, _incognitoVideoQuality);
    }

    private async void IncognitoVideoDownloadInstallEngine_Click(object sender, RoutedEventArgs e)
    {
        if (_incognitoVideoEngineInstallInProgress) return;

        _incognitoVideoEngineInstallInProgress = true;
        IncognitoVideoDownloadInstallEngineButton.IsEnabled = false;
        var progress = new Progress<string>(text => IncognitoVideoDownloadStatusText.Text = text);

        try
        {
            await YtDlpEngineProvider.DownloadEngineAsync(progress);
            RefreshIncognitoVideoEngineState();
            IncognitoVideoDownloadStatusText.Text = "Moteur installé. Prêt à télécharger cette vidéo YouTube.";
            IncognitoVideoDownloadStartButton.IsEnabled = _incognitoVideoCandidate is not null
                && _incognitoVideoCandidate.IsYouTube
                && !_incognitoVideoDownloadInProgress;
        }
        catch (Exception ex)
        {
            IncognitoVideoDownloadStatusText.Text = $"Installation du moteur impossible : {ex.Message}";
        }
        finally
        {
            _incognitoVideoEngineInstallInProgress = false;
            IncognitoVideoDownloadInstallEngineButton.IsEnabled = true;
        }
    }

    private async Task<IncognitoVideoDownloadCandidate?> DetectIncognitoVideoCandidateAsync()
    {
        var address = _currentTab?.View.CoreWebView2?.Source ?? _currentTab?.Address ?? IncognitoAddressBox.Text;
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var isYouTube = IsYouTubeHost(uri.Host);
        var youtubeId = isYouTube ? YouTubeVideoId(uri) : string.Empty;
        if (!isYouTube || string.IsNullOrWhiteSpace(youtubeId))
        {
            return null;
        }

        var title = _currentTab?.Title;
        var core = _currentTab?.View.CoreWebView2;
        if (core is not null)
        {
            try
            {
                var raw = await core.ExecuteScriptAsync("""
                    (function(){
                        var title = document.querySelector('h1 yt-formatted-string')?.textContent
                            || document.querySelector('meta[property="og:title"]')?.content
                            || document.title
                            || '';
                        return JSON.stringify({title:title.trim()});
                    })();
                    """);
                title = ParseVideoTitle(raw) ?? title;
            }
            catch { }
        }

        title = CleanYouTubeTitle(title, youtubeId);
        return new IncognitoVideoDownloadCandidate(title, $"https://www.youtube.com/watch?v={youtubeId}", true, youtubeId);
    }

    private async Task StartIncognitoYouTubeDownloadAsync(IncognitoVideoDownloadCandidate candidate, string enginePath, VideoQuality quality)
    {
        _incognitoVideoDownloadInProgress = true;
        IncognitoVideoDownloadStartButton.IsEnabled = false;
        IncognitoVideoDownloadProgressBar.Value = 0;
        IncognitoVideoDownloadProgressBar.IsIndeterminate = true;
        IncognitoVideoDownloadProgressBar.Visibility = Visibility.Visible;

        // Meme dossier que le reste de Lumora (Telechargements normal du profil) -
        // decision explicite de l'utilisateur, 2026-08-31 : le fichier survit à la
        // fermeture de cette fenêtre Incognito, comme dans Tor Browser officiel.
        var downloadsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        Directory.CreateDirectory(downloadsDir);

        var safeTitle = SafeFileName(candidate.Title);
        var outputTemplate = Path.Combine(downloadsDir, $"{safeTitle} [{candidate.YouTubeId}].%(ext)s");
        var startedAt = DateTimeOffset.Now;

        var outputLines = new List<string>();
        var errorLines = new List<string>();

        try
        {
            var qualityLabel = VideoDownloadFormat.Label(quality);
            IncognitoVideoDownloadStatusText.Text = $"Téléchargement YouTube en cours ({qualityLabel})...";

            var process = new Process
            {
                StartInfo = BuildYouTubeDownloadStartInfo(enginePath, candidate.Url, outputTemplate, FfmpegLocator.FindLocalFfmpeg(), quality),
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (_, args) =>
            {
                if (args.Data is null) return;
                outputLines.Add(args.Data);
                if (YtDlpProgress.TryParse(args.Data, out var percent, out var totalBytes))
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        IncognitoVideoDownloadProgressBar.IsIndeterminate = totalBytes <= 0;
                        IncognitoVideoDownloadProgressBar.Value = percent;
                        IncognitoVideoDownloadStatusText.Text = $"Téléchargement... {percent:0.0}%";
                    });
                }
            };
            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data is not null) errorLines.Add(args.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            var output = string.Join('\n', outputLines);
            var error = string.Join('\n', errorLines);

            if (process.ExitCode == 0)
            {
                var finalPath = FindDownloadedVideo(downloadsDir, candidate.YouTubeId, startedAt)
                                ?? ExtractLastExistingPath(output)
                                ?? downloadsDir;
                var fileName = File.Exists(finalPath) ? Path.GetFileName(finalPath) : $"{safeTitle}.mp4";
                IncognitoVideoDownloadStatusText.Text = $"Téléchargement terminé : {fileName}";
            }
            else
            {
                IncognitoVideoDownloadStatusText.Text = $"Échec du téléchargement : {LastUsefulLine(error) ?? "moteur vidéo indisponible."}";
            }
        }
        catch (Exception ex)
        {
            IncognitoVideoDownloadStatusText.Text = $"Téléchargement impossible : {ex.Message}";
        }
        finally
        {
            _incognitoVideoDownloadInProgress = false;
            IncognitoVideoDownloadProgressBar.Visibility = Visibility.Collapsed;
            IncognitoVideoDownloadStartButton.IsEnabled = _incognitoVideoCandidate is not null && YtDlpEngineProvider.FindLocalEngine() is not null;
        }
    }

    private static ProcessStartInfo BuildYouTubeDownloadStartInfo(string enginePath, string url, string outputTemplate, string? ffmpegPath, VideoQuality quality)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = enginePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add("--no-playlist");
        startInfo.ArgumentList.Add("--windows-filenames");
        startInfo.ArgumentList.Add("--restrict-filenames");
        startInfo.ArgumentList.Add("--newline");
        startInfo.ArgumentList.Add("--no-mtime");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add(VideoDownloadFormat.BuildFormatSelector(quality, ffmpegPath is not null));
        if (ffmpegPath is not null)
        {
            startInfo.ArgumentList.Add("--merge-output-format");
            startInfo.ArgumentList.Add("mp4");
            startInfo.ArgumentList.Add("--ffmpeg-location");
            startInfo.ArgumentList.Add(ffmpegPath);
        }
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(outputTemplate);
        startInfo.ArgumentList.Add(url);
        return startInfo;
    }

    private static bool IsYouTubeHost(string host) =>
        host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase);

    private static string YouTubeVideoId(Uri uri)
    {
        if (uri.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            return uri.AbsolutePath.Trim('/').Split('/')[0];
        }

        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in query)
        {
            var split = part.Split('=', 2);
            if (split.Length == 2 && split[0].Equals("v", StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(split[1]);
            }
        }

        return string.Empty;
    }

    private static string? ParseVideoTitle(string rawJson)
    {
        try
        {
            var node = JsonNode.Parse(rawJson);
            if (node is JsonValue value && value.TryGetValue<string>(out var nested))
            {
                node = JsonNode.Parse(nested);
            }

            return node?["title"]?.GetValue<string>();
        }
        catch
        {
            return null;
        }
    }

    private static string CleanYouTubeTitle(string? title, string fallbackId)
    {
        title = (title ?? string.Empty).Trim();
        if (title.EndsWith(" - YouTube", StringComparison.OrdinalIgnoreCase))
        {
            title = title[..^10].Trim();
        }

        return string.IsNullOrWhiteSpace(title) ? $"YouTube {fallbackId}" : title;
    }

    private static string SafeFileName(string title)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var safe = new string(title.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "video-youtube";
        }

        return safe.Length > 120 ? safe[..120].Trim() : safe;
    }

    private static string? FindDownloadedVideo(string downloadsDir, string videoId, DateTimeOffset startedAt)
    {
        try
        {
            return Directory.EnumerateFiles(downloadsDir)
                .Where(path => Path.GetFileName(path).Contains($"[{videoId}]", StringComparison.OrdinalIgnoreCase))
                .Where(path => File.GetLastWriteTime(path) >= startedAt.AddSeconds(-5).LocalDateTime)
                .OrderByDescending(File.GetLastWriteTime)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractLastExistingPath(string output)
    {
        foreach (var path in YtDlpOutputParser.CandidatePaths(output))
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static string? LastUsefulLine(string text) =>
        text.Split('\n')
            .Select(line => line.Trim())
            .LastOrDefault(line => !string.IsNullOrWhiteSpace(line));
}
