namespace Lumora.WinUI.Translation;

// Télécharge (à la demande, une seule fois par paire de langues) et met en
// cache les modèles de traduction, puis expose la traduction elle-même.
// Le téléchargement est un simple GET de fichiers génériques (aucune donnée
// utilisateur envoyée), sur le même principe que FilterListManager pour les
// listes de filtrage. La traduction proprement dite s'exécute ensuite
// entièrement hors ligne via TranslationEngine.
internal sealed class TranslationService : IDisposable
{
    private static readonly string ModelsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Lumora", "translation-models");

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(5),
        DefaultRequestHeaders = { { "User-Agent", "Lumora/1.0 (translation model downloader)" } }
    };

    private readonly Dictionary<string, TranslationEngine> _loadedEngines = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public bool IsPairSupported(string sourceLang, string targetLang) =>
        TranslationModelCatalog.Find(sourceLang, targetLang) is not null;

    public bool IsPairCached(string sourceLang, string targetLang)
    {
        var info = TranslationModelCatalog.Find(sourceLang, targetLang);
        if (info is null) return false;
        var dir = Path.Combine(ModelsDir, info.PairId);
        return TranslationModelCatalog.RequiredFiles.All(f => File.Exists(Path.Combine(dir, f)));
    }

    public async Task<string> TranslateAsync(
        string text, string sourceLang, string targetLang,
        IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var engine = await GetOrLoadEngineAsync(sourceLang, targetLang, progress, cancellationToken);
        return await engine.TranslateAsync(text, cancellationToken: cancellationToken);
    }

    private async Task<TranslationEngine> GetOrLoadEngineAsync(
        string sourceLang, string targetLang, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var info = TranslationModelCatalog.Find(sourceLang, targetLang)
            ?? throw new NotSupportedException($"Paire de langues non prise en charge : {sourceLang}->{targetLang}");

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            if (_loadedEngines.TryGetValue(info.PairId, out var cached)) return cached;

            var dir = Path.Combine(ModelsDir, info.PairId);
            await EnsureModelDownloadedAsync(info, dir, progress, cancellationToken);

            progress?.Report("Chargement du modele de traduction...");
            var engine = await TranslationEngine.LoadAsync(dir);
            _loadedEngines[info.PairId] = engine;
            return engine;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private static async Task EnsureModelDownloadedAsync(
        TranslationModelInfo info, string dir, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.Combine(dir, "onnx"));

        foreach (var relativePath in TranslationModelCatalog.RequiredFiles)
        {
            var destination = Path.Combine(dir, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(destination)) continue;

            progress?.Report($"Telechargement du modele de traduction ({info.SourceLang} -> {info.TargetLang}) : {relativePath}...");
            var url = $"https://huggingface.co/{info.HuggingFaceRepo}/resolve/main/{relativePath}";

            var tempPath = destination + ".part";
            using (var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = File.Create(tempPath);
                await input.CopyToAsync(output, cancellationToken);
            }
            File.Move(tempPath, destination, overwrite: true);
        }
    }

    public void Dispose()
    {
        foreach (var engine in _loadedEngines.Values) engine.Dispose();
        _loadedEngines.Clear();
    }
}
