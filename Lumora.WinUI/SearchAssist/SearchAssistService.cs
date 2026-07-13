using Microsoft.ML.OnnxRuntimeGenAI;

namespace Lumora.WinUI.SearchAssist;

// Assistant de reformulation de recherche, 100% local via un petit modele de
// langage (Phi-3-mini-4k-instruct, export ONNX quantifie pour CPU). Le
// modele (~2,7 Go) est telecharge une seule fois puis mis en cache ; la
// reformulation elle-meme s'execute entierement hors ligne. La seule donnee
// qui sort de la machine reste la requete finale que l'utilisateur choisit
// d'envoyer a son moteur de recherche — exactement comme sans cette fonction.
internal sealed class SearchAssistService : IDisposable
{
    private const string HuggingFaceRepo = "microsoft/Phi-3-mini-4k-instruct-onnx";
    private const string ModelSubPath = "cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4";
    private const string ModelFileName = "phi3-mini-4k-instruct-cpu-int4-rtn-block-32-acc-level-4.onnx";

    private static readonly string[] RequiredFiles =
    [
        "genai_config.json",
        ModelFileName,
        $"{ModelFileName}.data",
        "tokenizer.json",
        "tokenizer_config.json",
        "special_tokens_map.json",
    ];

    private static readonly string ModelDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Lumora", "search-assist-model");

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(60),
        DefaultRequestHeaders = { { "User-Agent", "Lumora/1.0 (search assist model downloader)" } }
    };

    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private OgaHandle? _ogaHandle;
    private Model? _model;
    private Tokenizer? _tokenizer;

    public bool IsModelCached() => RequiredFiles.All(f => File.Exists(Path.Combine(ModelDir, f)));

    public async Task<string?> RewriteQueryAsync(
        string rawQuery, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawQuery)) return null;

        await EnsureLoadedAsync(progress, cancellationToken);

        return await Task.Run(() => Generate(rawQuery), cancellationToken);
    }

    private async Task EnsureLoadedAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (_model is not null && _tokenizer is not null) return;

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            if (_model is not null && _tokenizer is not null) return;

            await EnsureModelDownloadedAsync(progress, cancellationToken);

            progress?.Report("Chargement du modele local...");
            _ogaHandle ??= new OgaHandle();
            _model = new Model(ModelDir);
            _tokenizer = new Tokenizer(_model);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private static async Task EnsureModelDownloadedAsync(
        IProgress<string>? progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(ModelDir);

        foreach (var fileName in RequiredFiles)
        {
            var destination = Path.Combine(ModelDir, fileName);
            if (File.Exists(destination)) continue;

            progress?.Report($"Telechargement du modele de recherche assistee : {fileName}...");
            var url = $"https://huggingface.co/{HuggingFaceRepo}/resolve/main/{ModelSubPath}/{fileName}";

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

    private string? Generate(string rawQuery)
    {
        if (_model is null || _tokenizer is null) return null;

        var prompt =
            "<|system|>\n" +
            "Tu reformules une requete de recherche web courte et parfois maladroite en une " +
            "requete plus precise et complete, en francais. Reponds uniquement avec la requete " +
            "reformulee, sur une seule ligne, sans guillemets et sans aucune explication.<|end|>\n" +
            $"<|user|>\n{rawQuery}<|end|>\n<|assistant|>\n";

        var sequences = _tokenizer.Encode(prompt);

        using var generatorParams = new GeneratorParams(_model);
        generatorParams.SetSearchOption("max_length", 96);

        using var generator = new Generator(_model, generatorParams);
        generator.AppendTokenSequences(sequences);

        using var stream = _tokenizer.CreateStream();
        var sb = new System.Text.StringBuilder();
        while (!generator.IsDone())
        {
            generator.GenerateNextToken();
            foreach (var token in generator.GetNextTokens())
                sb.Append(stream.Decode(token));
        }

        var firstLine = sb.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        return firstLine?.Trim('"', ' ', '«', '»');
    }

    public void Dispose()
    {
        _tokenizer?.Dispose();
        _model?.Dispose();
        _ogaHandle?.Dispose();
    }
}
