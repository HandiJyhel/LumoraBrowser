using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Lumora.WinUI.Translation;

namespace Lumora.WinUI;

// ── Traduction locale de pages ───────────────────────────────────────────
// Bandeau "Traduire cette page ?" quand la langue detectee de la page differe
// du francais. Traduction neuronale 100% locale (TranslationService) : seul
// le fichier de modele generique est telecharge une fois, jamais de texte de
// page envoye a un serveur externe.
public sealed partial class MainWindow
{
    private readonly TranslationService _translationService = new();
    private bool _translationInProgress;

    private sealed record PageTextBlock(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("text")] string Text);

    private async Task OfferTranslationIfNeededAsync(WebView2 sender, string address)
    {
        if (!_uiSettings.TranslationEnabled || !BookmarkStore.IsWebUrl(address)) return;

        var core = sender.CoreWebView2;
        if (core is null) return;

        string? detected = null;
        try
        {
            var raw = await core.ExecuteScriptAsync(await LoadTranslationScriptAsync("DetectLanguageScript.js"));
            detected = JsonSerializer.Deserialize<string>(raw);
        }
        catch { }

        if (!IsActiveView(sender)) return;

        if (!string.IsNullOrEmpty(detected) && _translationService.IsPairSupported(detected, "fr"))
        {
            _pendingTranslationSourceLang = detected;
            TranslateText.Text = detected == "en"
                ? "Cette page semble être en anglais. La traduire en français ?"
                : $"Cette page semble être dans une autre langue ({detected}). La traduire en français ?";
            TranslateBar.Visibility = Visibility.Visible;
        }
        else
        {
            TranslateBar.Visibility = Visibility.Collapsed;
        }
    }

    private string? _pendingTranslationSourceLang;

    private void TranslateDismiss_Click(object sender, RoutedEventArgs e) =>
        TranslateBar.Visibility = Visibility.Collapsed;

    private async void TranslateAccept_Click(object sender, RoutedEventArgs e)
    {
        if (_translationInProgress || _pendingTranslationSourceLang is null) return;
        var sourceLang = _pendingTranslationSourceLang;

        var core = _browserView?.CoreWebView2;
        if (core is null) return;

        _translationInProgress = true;
        TranslateAccept.IsEnabled = false;
        StatusText.Text = "Traduction de la page en cours...";
        try
        {
            var extractScript = await LoadTranslationScriptAsync("PageTextExtractScript.js");
            var rawBlocks = await core.ExecuteScriptAsync(extractScript);
            var blocks = JsonSerializer.Deserialize<List<PageTextBlock>>(rawBlocks,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

            var translations = new Dictionary<string, string>();
            var done = 0;
            foreach (var block in blocks)
            {
                if (string.IsNullOrWhiteSpace(block.Text)) continue;
                try
                {
                    var translated = await _translationService.TranslateAsync(block.Text, sourceLang, "fr");
                    if (!string.IsNullOrWhiteSpace(translated))
                        translations[block.Id] = translated;
                }
                catch { }
                done++;
                if (done % 5 == 0)
                    StatusText.Text = $"Traduction de la page en cours... ({done}/{blocks.Count})";
            }

            var applyScript = await LoadTranslationScriptAsync("PageTextApplyScript.js");
            var payload = JsonSerializer.Serialize(translations);
            var executable = applyScript.Replace("__NOVA_TRANSLATIONS__", payload);
            await core.ExecuteScriptAsync(executable);

            StatusText.Text = $"Page traduite ({translations.Count} bloc(s) de texte).";
            TranslateBar.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Erreur lors de la traduction : {ex.Message}";
        }
        finally
        {
            _translationInProgress = false;
            TranslateAccept.IsEnabled = true;
        }
    }

    private static async Task<string> LoadTranslationScriptAsync(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Translation", fileName),
            Path.Combine(Environment.CurrentDirectory, "Lumora.WinUI", "Translation", fileName),
            Path.Combine(Environment.CurrentDirectory, "Translation", fileName),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return await File.ReadAllTextAsync(path);
        }

        throw new FileNotFoundException($"Script de traduction introuvable : {fileName}");
    }
}
