using System.Diagnostics;
using System.Text.Json.Nodes;
using Lumora.WinUI.VideoDownload;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    private sealed record VideoDownloadCandidate(
        string Title,
        string Url,
        bool IsYouTube,
        string YouTubeId);

    private VideoDownloadCandidate? _videoDownloadCandidate;
    private bool _videoDownloadInProgress;
    private bool _videoEngineInstallInProgress;
    private VideoQuality _videoDownloadQuality = VideoQuality.Best;

    private void VideoDownloadMenu_Click(object sender, RoutedEventArgs e) =>
        ShowVideoDownloadFlyout(VideoDownloadButton);

    private void ShowVideoDownloadFlyout(FrameworkElement flyoutTarget)
    {
        VideoDownloadFlyout.ShowAt(flyoutTarget);
    }

    private async void VideoDownloadFlyout_Opening(object sender, object e)
    {
        VideoDownloadStartButton.IsEnabled = false;
        VideoDownloadQualityCombo.IsEnabled = false;
        VideoDownloadInstallEngineButton.Visibility = Visibility.Collapsed;
        VideoDownloadTitleText.Text = "Analyse de la page...";
        VideoDownloadStatusText.Text = string.Empty;
        _videoDownloadCandidate = null;

        var candidate = await DetectVideoDownloadCandidateAsync();
        _videoDownloadCandidate = candidate;

        RefreshVideoEngineState();

        if (candidate is null)
        {
            VideoDownloadTitleText.Text = "Aucune vidéo YouTube détectée sur cet onglet.";
            VideoDownloadStatusText.Text = "Ouvre une page vidéo YouTube, puis relance ce module.";
            return;
        }

        VideoDownloadTitleText.Text = candidate.Title;
        if (!candidate.IsYouTube)
        {
            VideoDownloadStatusText.Text = "Cette première version du module cible les pages vidéo YouTube.";
            return;
        }

        var enginePath = YtDlpEngineProvider.FindLocalEngine();
        VideoDownloadStartButton.IsEnabled = enginePath is not null && !_videoDownloadInProgress;
        VideoDownloadQualityCombo.IsEnabled = enginePath is not null && !_videoDownloadInProgress;
        VideoDownloadStatusText.Text = enginePath is null
            ? "Installe le moteur pour activer le téléchargement de cette vidéo."
            : FfmpegLocator.FindLocalFfmpeg() is null
                ? "Prêt à télécharger (qualité limitée : ffmpeg absent pour fusionner les flux au-delà de 720p)."
                : "Prêt à télécharger cette vidéo YouTube dans la qualité choisie ci-dessous.";
    }

    private void VideoDownloadQualityCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VideoDownloadQualityCombo.SelectedItem is ComboBoxItem item
            && item.Tag is string tag
            && Enum.TryParse<VideoQuality>(tag, out var quality))
        {
            _videoDownloadQuality = quality;
        }
    }

    // Met a jour l'affichage (texte moteur + bouton d'installation) sans relancer
    // la detection de la page. Appelee a l'ouverture du flyout et apres une
    // installation reussie du moteur.
    private void RefreshVideoEngineState()
    {
        var enginePath = YtDlpEngineProvider.FindLocalEngine();
        VideoDownloadEngineText.Text = enginePath is null
            ? "Moteur YouTube local introuvable."
            : $"Moteur local détecté : {Path.GetFileName(enginePath)}";

        VideoDownloadInstallEngineButton.Visibility = enginePath is null
            ? Visibility.Visible
            : Visibility.Collapsed;
        VideoDownloadInstallEngineButton.IsEnabled = !_videoEngineInstallInProgress;
    }

    private async void VideoDownloadStartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_videoDownloadInProgress)
        {
            VideoDownloadStatusText.Text = "Un téléchargement vidéo est déjà en cours.";
            return;
        }

        var candidate = _videoDownloadCandidate;
        if (candidate is null || !candidate.IsYouTube)
        {
            VideoDownloadStatusText.Text = "Aucune vidéo YouTube active.";
            return;
        }

        var enginePath = YtDlpEngineProvider.FindLocalEngine();
        if (enginePath is null)
        {
            VideoDownloadStartButton.IsEnabled = false;
            VideoDownloadStatusText.Text = "Moteur local introuvable : installe-le d'abord avec le bouton ci-dessus.";
            RefreshVideoEngineState();
            return;
        }

        await StartYouTubeDownloadAsync(candidate, enginePath, _videoDownloadQuality);
    }

    private async void VideoDownloadInstallEngine_Click(object sender, RoutedEventArgs e)
    {
        if (_videoEngineInstallInProgress)
        {
            return;
        }

        _videoEngineInstallInProgress = true;
        VideoDownloadInstallEngineButton.IsEnabled = false;
        var progress = new Progress<string>(text => VideoDownloadStatusText.Text = text);

        try
        {
            await YtDlpEngineProvider.DownloadEngineAsync(progress);
            RefreshVideoEngineState();
            VideoDownloadStatusText.Text = "Moteur installé. Prêt à télécharger cette vidéo YouTube.";
            VideoDownloadStartButton.IsEnabled = _videoDownloadCandidate is not null
                && _videoDownloadCandidate.IsYouTube
                && !_videoDownloadInProgress;
        }
        catch (Exception ex)
        {
            VideoDownloadStatusText.Text = $"Installation du moteur impossible : {ex.Message}";
        }
        finally
        {
            _videoEngineInstallInProgress = false;
            VideoDownloadInstallEngineButton.IsEnabled = true;
        }
    }

    private async Task<VideoDownloadCandidate?> DetectVideoDownloadCandidateAsync()
    {
        var tab = CurrentTab();
        var address = tab?.View?.Source?.ToString() ?? tab?.Address ?? AddressBox.Text;
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

        var title = tab?.Title;
        var core = tab?.View?.CoreWebView2;
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
        return new VideoDownloadCandidate(title, $"https://www.youtube.com/watch?v={youtubeId}", true, youtubeId);
    }

    private async Task StartYouTubeDownloadAsync(VideoDownloadCandidate candidate, string enginePath, VideoQuality quality)
    {
        _videoDownloadInProgress = true;
        VideoDownloadStartButton.IsEnabled = false;
        VideoDownloadProgressBar.Value = 0;
        VideoDownloadProgressBar.IsIndeterminate = true;
        VideoDownloadProgressBar.Visibility = Visibility.Visible;

        var downloadsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        Directory.CreateDirectory(downloadsDir);

        var safeTitle = SafeFileName(candidate.Title);
        var outputTemplate = Path.Combine(downloadsDir, $"{safeTitle} [{candidate.YouTubeId}].%(ext)s");
        var startedAt = DateTimeOffset.Now;
        var historyId = DownloadHistoryEntry.NewId();
        var initialEntry = new DownloadHistoryEntry(
            historyId,
            $"{safeTitle}.mp4",
            candidate.Url,
            DownloadHistoryEntry.SourceDomainFor(candidate.Url),
            downloadsDir,
            0,
            0,
            DownloadHistoryEntry.InProgress,
            startedAt,
            null);
        _historyPanel.Downloads.Upsert(initialEntry);
        RenderDownloads();

        var outputLines = new List<string>();
        var errorLines = new List<string>();

        try
        {
            var qualityLabel = VideoDownloadFormat.Label(quality);
            VideoDownloadStatusText.Text = $"Téléchargement YouTube en cours ({qualityLabel})...";
            StatusText.Text = $"Téléchargement vidéo démarré : {candidate.Title}";

            var process = new Process
            {
                StartInfo = BuildYouTubeDownloadStartInfo(enginePath, candidate.Url, outputTemplate, FfmpegLocator.FindLocalFfmpeg(), quality),
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (_, args) =>
            {
                if (args.Data is null)
                {
                    return;
                }

                outputLines.Add(args.Data);
                if (YtDlpProgress.TryParse(args.Data, out var percent, out var totalBytes))
                {
                    var receivedBytes = totalBytes > 0 ? (long)(percent / 100.0 * totalBytes) : 0;
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        _historyPanel.Downloads.Upsert(initialEntry.WithProgress(receivedBytes, totalBytes));
                        RenderDownloads();
                        VideoDownloadProgressBar.IsIndeterminate = totalBytes <= 0;
                        VideoDownloadProgressBar.Value = percent;
                        VideoDownloadStatusText.Text = $"Téléchargement... {percent:0.0}%";
                    });
                }
            };
            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                {
                    errorLines.Add(args.Data);
                }
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
                _historyPanel.Downloads.Upsert(initialEntry with
                {
                    FileName = fileName,
                    LocalPath = finalPath,
                    State = DownloadHistoryEntry.Completed,
                    CompletedAt = DateTimeOffset.Now
                });
                VideoDownloadStatusText.Text = $"Téléchargement terminé : {fileName}";
                StatusText.Text = $"Vidéo téléchargée : {fileName}";
            }
            else
            {
                _historyPanel.Downloads.Upsert(initialEntry with
                {
                    State = DownloadHistoryEntry.Failed,
                    CompletedAt = DateTimeOffset.Now
                });
                VideoDownloadStatusText.Text = $"Échec du téléchargement : {LastUsefulLine(error) ?? "moteur vidéo indisponible."}";
                StatusText.Text = "Téléchargement vidéo échoué.";
            }

            RenderDownloads();
        }
        catch (Exception ex)
        {
            _historyPanel.Downloads.Upsert(initialEntry with
            {
                State = DownloadHistoryEntry.Failed,
                CompletedAt = DateTimeOffset.Now
            });
            RenderDownloads();
            VideoDownloadStatusText.Text = $"Téléchargement impossible : {ex.Message}";
            StatusText.Text = "Téléchargement vidéo impossible.";
        }
        finally
        {
            _videoDownloadInProgress = false;
            VideoDownloadProgressBar.Visibility = Visibility.Collapsed;
            VideoDownloadStartButton.IsEnabled = _videoDownloadCandidate is not null && YtDlpEngineProvider.FindLocalEngine() is not null;
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
        // yt-dlp regle par defaut la date de modification du fichier sur le
        // Last-Modified/upload date de la video, pas sur l'heure reelle du
        // telechargement : sans --no-mtime, FindDownloadedVideo (filtre par
        // date >= debut du telechargement) ne retrouve jamais le fichier ecrit.
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
