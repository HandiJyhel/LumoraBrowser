using Lumora.WinUI.ModelDownload;

namespace Lumora.WinUI.SemanticSearch;

// Telecharge (a la demande, une seule fois) et met en cache le modele
// d'embedding, puis expose le calcul d'embedding lui-meme. Meme principe que
// TranslationService/SearchAssistService : simple GET de fichiers generiques,
// aucune donnee utilisateur envoyee pour les telecharger. Le calcul
// d'embedding proprement dit s'execute ensuite entierement hors ligne via
// EmbeddingEngine.
internal sealed class EmbeddingService : IDisposable
{
    private static readonly string ModelDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Lumora", "semantic-search-model");

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(5),
        DefaultRequestHeaders = { { "User-Agent", "Lumora/1.0 (embedding model downloader)" } }
    };

    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private EmbeddingEngine? _engine;

    public bool IsModelCached() =>
        EmbeddingModelCatalog.RequiredFiles.All(f => File.Exists(Path.Combine(ModelDir, f.RelativePath.Replace('/', Path.DirectorySeparatorChar))));

    public async Task<float[]> EmbedAsync(
        string text, bool isQuery, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var engine = await GetOrLoadEngineAsync(progress, cancellationToken);
        return await engine.EmbedAsync(text, isQuery, cancellationToken);
    }

    private async Task<EmbeddingEngine> GetOrLoadEngineAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            if (_engine is not null) return _engine;

            await EnsureModelDownloadedAsync(progress, cancellationToken);

            progress?.Report("Chargement du modele de recherche semantique...");
            _engine = await EmbeddingEngine.LoadAsync(ModelDir);
            return _engine;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private static async Task EnsureModelDownloadedAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.Combine(ModelDir, "onnx"));

        foreach (var file in EmbeddingModelCatalog.RequiredFiles)
        {
            var destination = Path.Combine(ModelDir, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(destination)) continue;

            progress?.Report($"Telechargement du modele de recherche semantique : {file.RelativePath}...");
            var url = $"https://huggingface.co/{EmbeddingModelCatalog.HuggingFaceRepo}/resolve/main/{file.RelativePath}";

            var tempPath = destination + ".part";
            using (var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = File.Create(tempPath);
                await input.CopyToAsync(output, cancellationToken);
            }

            progress?.Report($"Verification de l'integrite : {file.RelativePath}...");
            await ModelIntegrity.VerifyOrDeleteAsync(tempPath, file.Sha256, cancellationToken);
            File.Move(tempPath, destination, overwrite: true);
        }
    }

    public void Dispose()
    {
        _engine?.Dispose();
        _engine = null;
    }
}
