using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Lumora.WinUI.ReadAloud;

namespace Lumora.WinUI;

// ── Lecture a voix haute (accessibilite) ─────────────────────────────────
// Synthese vocale locale (ReadAloudService) du texte de la page active.
// Jamais automatique : uniquement sur clic. Le texte de la page ne quitte
// jamais la machine (aucun appel reseau pour la synthese elle-meme).
public sealed partial class MainWindow
{
    private const string ReadAloudPlayGlyph = "";
    private const string ReadAloudPauseGlyph = "";
    private const string ReadAloudVolumeGlyph = "";

    private ReadAloudService? _readAloudServiceField;
    private ReadAloudService _readAloudService => _readAloudServiceField ??= CreateReadAloudService();

    private sealed record PageTextBlockForReading([property: JsonPropertyName("text")] string Text);

    private ReadAloudService CreateReadAloudService()
    {
        var service = new ReadAloudService(DispatcherQueue);
        service.StateChanged += OnReadAloudStateChanged;
        return service;
    }

    private void UpdateReadAloudButtonVisibility()
    {
        if (ReadAloudButton is null) return;
        ReadAloudButton.Opacity = _uiSettings.ReadAloudEnabled ? 1 : 0.72;
        UpdateModulesPinUi();
    }

    private void OnReadAloudStateChanged(ReadAloudState state)
    {
        ReadAloudPlayPauseButton.Content = state == ReadAloudState.Playing ? "Pause" : "Lire la page";
        ReadAloudStatusText.Text = state switch
        {
            ReadAloudState.Playing => "Lecture en cours...",
            ReadAloudState.Paused => "En pause.",
            _ => "Pret.",
        };
        ReadAloudIcon.Glyph = state switch
        {
            ReadAloudState.Playing => ReadAloudPauseGlyph,
            ReadAloudState.Paused => ReadAloudPlayGlyph,
            _ => ReadAloudVolumeGlyph,
        };
    }

    private void ReadAloudButton_Click(object sender, RoutedEventArgs e)
    {
        // Le clic ouvre juste le Flyout (comportement par defaut du bouton) ;
        // rien a faire ici, geré par les boutons du Flyout.
    }

    private async void ReadAloudPlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_uiSettings.ReadAloudEnabled) return;

        if (_readAloudService.State == ReadAloudState.Playing)
        {
            _readAloudService.Pause();
            return;
        }

        if (_readAloudService.State == ReadAloudState.Paused)
        {
            _readAloudService.Resume();
            return;
        }

        var core = _browserView?.CoreWebView2;
        if (core is null) return;

        try
        {
            ReadAloudStatusText.Text = "Extraction du texte...";
            var extractScript = await LoadReadAloudScriptAsync("PageTextExtractScript.js");
            var rawBlocks = await core.ExecuteScriptAsync(extractScript);
            var blocks = JsonSerializer.Deserialize<List<PageTextBlockForReading>>(rawBlocks,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

            var chunks = blocks.Select(b => b.Text).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
            if (chunks.Count == 0)
            {
                ReadAloudStatusText.Text = "Aucun texte lisible trouve sur cette page.";
                return;
            }

            _readAloudService.Start(chunks);
        }
        catch (Exception ex)
        {
            ReadAloudStatusText.Text = $"Lecture indisponible : {ex.Message}";
        }
    }

    private void ReadAloudStopButton_Click(object sender, RoutedEventArgs e) =>
        _readAloudService.Stop();

    private static async Task<string> LoadReadAloudScriptAsync(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "ReadAloud", fileName),
            Path.Combine(Environment.CurrentDirectory, "Lumora.WinUI", "ReadAloud", fileName),
            Path.Combine(Environment.CurrentDirectory, "ReadAloud", fileName),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return await File.ReadAllTextAsync(path);
        }

        throw new FileNotFoundException($"Script de lecture introuvable : {fileName}");
    }
}
