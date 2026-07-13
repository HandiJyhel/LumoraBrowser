using System.Diagnostics;
using System.Text.Json.Nodes;
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

    private void VideoDownloadMenu_Click(object sender, RoutedEventArgs e)
    {
        VideoDownloadFlyout.ShowAt(VideoDownloadButton);
    }

    private async void VideoDownloadFlyout_Opening(object sender, object e)
    {
        VideoDownloadStartButton.IsEnabled = false;
        VideoDownloadTitleText.Text = "Analyse de la page...";
        VideoDownloadStatusText.Text = string.Empty;
        _videoDownloadCandidate = null;

        var candidate = await DetectVideoDownloadCandidateAsync();
        _videoDownloadCandidate = candidate;

        var enginePath = FindYouTubeDownloadEngine();
        VideoDownloadEngineText.Text = enginePath is null
            ? "Moteur YouTube local introuvable. Place yt-dlp.exe dans le dossier de Lumora, dans %LOCALAPPDATA%\\Lumora\\tools, ou dans le PATH."
            : $"Moteur local detecte : {Path.GetFileName(enginePath)}";

        if (candidate is null)
        {
            VideoDownloadTitleText.Text = "Aucune video YouTube detectee sur cet onglet.";
            VideoDownloadStatusText.Text = "Ouvre une page video YouTube, puis relance ce module.";
            return;
        }

        VideoDownloadTitleText.Text = candidate.Title;
        if (!candidate.IsYouTube)
        {
            VideoDownloadStatusText.Text = "Cette premiere version du module cible les pages video YouTube.";
            return;
        }

        VideoDownloadStartButton.IsEnabled = enginePath is not null && !_videoDownloadInProgress;
        VideoDownloadStatusText.Text = enginePath is null
            ? "Le bouton est pret, mais il manque le moteur local de telechargement."
            : "Pret a telecharger cette video YouTube dans le dossier Telechargements.";
    }

    private async void VideoDownloadStartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_videoDownloadInProgress)
        {
            VideoDownloadStatusText.Text = "Un telechargement video est deja en cours.";
            return;
        }

        var candidate = _videoDownloadCandidate;
        if (candidate is null || !candidate.IsYouTube)
        {
            VideoDownloadStatusText.Text = "Aucune video YouTube active.";
            return;
        }

        var enginePath = FindYouTubeDownloadEngine();
        if (enginePath is null)
        {
            VideoDownloadStartButton.IsEnabled = false;
            VideoDownloadStatusText.Text = "Moteur local introuvable : ajoute yt-dlp.exe dans le dossier de Lumora ou dans le PATH.";
            return;
        }

        await StartYouTubeDownloadAsync(candidate, enginePath);
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

    private async Task StartYouTubeDownloadAsync(VideoDownloadCandidate candidate, string enginePath)
    {
        _videoDownloadInProgress = true;
        VideoDownloadStartButton.IsEnabled = false;

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

        try
        {
            VideoDownloadStatusText.Text = "Telechargement YouTube en cours...";
            StatusText.Text = $"Telechargement video demarre: {candidate.Title}";

            var process = new Process
            {
                StartInfo = BuildYouTubeDownloadStartInfo(enginePath, candidate.Url, outputTemplate),
                EnableRaisingEvents = true
            };

            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var output = await outputTask;
            var error = await errorTask;

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
                VideoDownloadStatusText.Text = $"Telechargement termine : {fileName}";
                StatusText.Text = $"Video telechargee: {fileName}";
            }
            else
            {
                _historyPanel.Downloads.Upsert(initialEntry with
                {
                    State = DownloadHistoryEntry.Failed,
                    CompletedAt = DateTimeOffset.Now
                });
                VideoDownloadStatusText.Text = $"Echec du telechargement : {LastUsefulLine(error) ?? "moteur video indisponible."}";
                StatusText.Text = "Telechargement video echoue.";
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
            VideoDownloadStatusText.Text = $"Telechargement impossible : {ex.Message}";
            StatusText.Text = "Telechargement video impossible.";
        }
        finally
        {
            _videoDownloadInProgress = false;
            VideoDownloadStartButton.IsEnabled = _videoDownloadCandidate is not null && FindYouTubeDownloadEngine() is not null;
        }
    }

    private static ProcessStartInfo BuildYouTubeDownloadStartInfo(string enginePath, string url, string outputTemplate)
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
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("best[ext=mp4]/best");
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(outputTemplate);
        startInfo.ArgumentList.Add(url);
        return startInfo;
    }

    private static string? FindYouTubeDownloadEngine()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("LUMORA_YTDLP_PATH") ?? string.Empty,
            Path.Combine(AppContext.BaseDirectory, "tools", "yt-dlp.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumora", "tools", "yt-dlp.exe")
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
            {
                return candidate;
            }
        }

        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim(), "yt-dlp.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch { }
        }

        return null;
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
        foreach (var line in output.Split('\n').Reverse())
        {
            var marker = "Destination: ";
            var index = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                continue;
            }

            var path = line[(index + marker.Length)..].Trim();
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
