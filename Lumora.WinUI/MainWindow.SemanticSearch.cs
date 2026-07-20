using System.Text.Json;
using Lumora.WinUI.SemanticSearch;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace Lumora.WinUI;

// Recherche semantique locale dans l'historique : capture le contenu texte des
// pages visitees, calcule un embedding local (EmbeddingService) et l'indexe
// (SemanticHistoryIndex) pour permettre une recherche par sens plutot que par
// mot-clé exact. Entierement opt-in (UiSettings.HistorySemanticSearchEnabled),
// desactive par defaut - voir le toggle "Recherche intelligente" dans
// Reglages > Espace de travail.
public sealed partial class MainWindow
{
    private readonly EmbeddingService _embeddingService = new();
    private bool _semanticSelfCheckDone;

    private async void CaptureSemanticContentAsync(WebView2? view, string url, string title)
    {
        if (_isGuestMode) return;
        if (!_uiSettings.HistorySemanticSearchEnabled) return;
        if (!BookmarkStore.IsWebUrl(url)) return;

        var core = view?.CoreWebView2;
        if (view is null || core is null) return;

        try
        {
            var script = await LoadSemanticSearchScriptAsync("SemanticContentExtractScript.js");
            var raw = await core.ExecuteScriptAsync(script);
            var text = JsonSerializer.Deserialize<string>(raw) ?? string.Empty;
            // Page trop pauvre en texte (accueil d'appli, redirection...) :
            // pas la peine d'indexer, ca ne ferait que bruiter la recherche.
            if (text.Length < 40) return;

            var progress = new Progress<string>(message => DispatcherQueue.TryEnqueue(() => UpdateStatusText(message, announce: false)));
            var embedding = await _embeddingService.EmbedAsync(text, isQuery: false, progress);

            await EnsureSemanticSelfCheckAsync(progress);

            var snippet = text.Length > 240 ? text[..240] : text;
            _semanticIndex.Upsert(url, title, snippet, embedding);
        }
        catch (Exception error)
        {
            WinUiRuntimeTrace.Write($"Semantic capture skipped: {error.GetType().Name}");
        }
    }

    // Recherche en langage naturel : encode la requete de l'utilisateur avec
    // le prefixe "query: " (asymetrique du prefixe "passage: " utilise pour le
    // contenu indexe - convention du modele E5, ce n'est pas interchangeable)
    // puis classe l'index par similarite cosinus.
    private async Task<IReadOnlyList<HistoryListItem>> SearchHistorySemanticAsync(string query, CancellationToken cancellationToken = default)
    {
        if (!_uiSettings.HistorySemanticSearchEnabled || string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        try
        {
            var queryEmbedding = await _embeddingService.EmbedAsync(query, isQuery: true, cancellationToken: cancellationToken);
            var results = _semanticIndex.Search(queryEmbedding);
            return results
                .Select(r => HistoryListItemFor(new HistoryEntry(r.Entry.Url, r.Entry.Title, r.Entry.IndexedAt)))
                .ToList();
        }
        catch (Exception error)
        {
            WinUiRuntimeTrace.Write($"Semantic search skipped: {error.GetType().Name}");
            return [];
        }
    }

    // Garde-fou execute une seule fois par session, au tout premier embedding
    // reellement calcule : verifie que deux phrases proches en sens obtiennent
    // une similarite nettement plus haute que deux phrases sans rapport. Voir
    // le commentaire de tete de EmbeddingEngine.cs sur la zone de risque
    // (decalage de tokenisation XLM-R) que ce test permet de detecter sans
    // avoir a inspecter manuellement chaque embedding.
    private async Task EnsureSemanticSelfCheckAsync(IProgress<string>? progress)
    {
        if (_semanticSelfCheckDone) return;
        _semanticSelfCheckDone = true;

        try
        {
            var a = await _embeddingService.EmbedAsync("Le chat dort sur le canape.", isQuery: false, progress);
            var b = await _embeddingService.EmbedAsync("Un chat fait la sieste sur le sofa.", isQuery: false, progress);
            var c = await _embeddingService.EmbedAsync("La bourse europeenne a chute ce matin.", isQuery: false, progress);

            var similar = Dot(a, b);
            var different = Dot(a, c);
            WinUiRuntimeTrace.Write($"Semantic self-check: similar={similar:0.000} different={different:0.000}");
            if (similar <= different)
            {
                WinUiRuntimeTrace.Write("Semantic self-check FAILED: le tokenizer produit probablement des embeddings incoherents (voir EmbeddingEngine.cs).");
            }
        }
        catch (Exception error)
        {
            WinUiRuntimeTrace.Write($"Semantic self-check skipped: {error.GetType().Name}");
        }
    }

    private static float Dot(float[] a, float[] b)
    {
        var sum = 0f;
        for (var i = 0; i < a.Length && i < b.Length; i++) sum += a[i] * b[i];
        return sum;
    }

    private static async Task<string> LoadSemanticSearchScriptAsync(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "SemanticSearch", fileName),
            Path.Combine(Environment.CurrentDirectory, "Lumora.WinUI", "SemanticSearch", fileName),
            Path.Combine(Environment.CurrentDirectory, "SemanticSearch", fileName),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return await File.ReadAllTextAsync(path);
            }
        }

        throw new FileNotFoundException($"Script introuvable : {fileName}");
    }
}
