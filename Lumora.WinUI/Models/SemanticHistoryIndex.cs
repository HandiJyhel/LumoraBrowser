using System.Text.Json;

namespace Lumora.WinUI;

public sealed record SemanticIndexEntry(string Url, string Title, DateTimeOffset IndexedAt, string Snippet, float[] Embedding);

public sealed record SemanticSearchResult(SemanticIndexEntry Entry, float Score);

// Index de recherche semantique locale, en complement de HistoryStore (pas a
// la place). Meme protection que l'historique classique : JSON chiffre DPAPI
// via LumoraFile, meme plafond d'entrees - le contenu de page capture ici
// (Snippet) est potentiellement plus sensible que le simple titre/URL de
// l'historique, donc au minimum le meme niveau de protection, jamais moins.
//
// Recherche par force brute (produit scalaire, les embeddings sont deja
// normalises L2 a l'encodage donc similarite cosinus = produit scalaire) :
// largement suffisant pour un historique de navigation personnel (au plus
// quelques milliers d'entrees, calcul sous la milliseconde), pas besoin d'un
// index vectoriel specialise.
internal sealed class SemanticHistoryIndex
{
    private const int MaxEntries = 2000;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };
    private readonly string _indexFile;
    private readonly List<SemanticIndexEntry> _entries = new();
    private bool _isGuest;

    public SemanticHistoryIndex(string indexFile)
    {
        _indexFile = indexFile;
        Load();
    }

    public void SetGuestMode(bool isGuest)
    {
        _isGuest = isGuest;
        if (isGuest) _entries.Clear();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_indexFile)) return;
            var json = LumoraFile.TryReadAllText(_indexFile);
            if (json is null) return;
            var loaded = JsonSerializer.Deserialize<List<SemanticIndexEntry>>(json, JsonOpts);
            if (loaded is not null) _entries.AddRange(loaded);
        }
        catch { }
    }

    public void Upsert(string url, string title, string snippet, float[] embedding)
    {
        if (_isGuest) return;

        _entries.RemoveAll(e => string.Equals(e.Url, url, StringComparison.Ordinal));
        _entries.Insert(0, new SemanticIndexEntry(
            url,
            string.IsNullOrWhiteSpace(title) ? url : title,
            DateTimeOffset.Now,
            snippet,
            embedding));
        while (_entries.Count > MaxEntries)
        {
            _entries.RemoveAt(_entries.Count - 1);
        }

        Save();
    }

    public void RemoveByUrl(string url)
    {
        _entries.RemoveAll(e => string.Equals(e.Url, url, StringComparison.Ordinal));
        Save();
    }

    public void Clear()
    {
        _entries.Clear();
        Save();
    }

    public bool HasEntryFor(string url) =>
        _entries.Any(e => string.Equals(e.Url, url, StringComparison.Ordinal));

    // minScore : mesure reellement sur ce modele (harnais de verification hors
    // Lumora, 2026-07-20, avant integration) plutot que suppose. Avec
    // multilingual-e5-small, deux textes SANS rapport tournent deja autour de
    // 0.78-0.85 de similarite cosinus (le modele reste "confiant" sur la
    // structure linguistique commune), un vrai rapprochement semantique monte
    // plutot vers 0.88-0.94. 0.85 est donc le plancher choisi pour limiter les
    // faux positifs - a ajuster si l'usage reel (vocabulaire de pages tres
    // varie) montre trop/trop peu de resultats.
    public IReadOnlyList<SemanticSearchResult> Search(float[] queryEmbedding, int topK = 8, float minScore = 0.85f)
    {
        return _entries
            .Select(e => new SemanticSearchResult(e, CosineSimilarity(e.Embedding, queryEmbedding)))
            .Where(r => r.Score >= minScore)
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0f;
        var dot = 0f;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
        }

        return dot;
    }

    private void Save()
    {
        if (_isGuest) return;
        try
        {
            LumoraFile.WriteAllText(_indexFile, JsonSerializer.Serialize(_entries, JsonOpts));
        }
        catch { }
    }
}
