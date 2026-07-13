using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace Lumora.WinUI.Translation;

// Moteur de traduction neuronale locale (famille OPUS-MT / Marian, export ONNX
// "decoder_model_merged"). Décodage glouton (pas de recherche en faisceau) :
// plus simple et plus sûr à faire tourner correctement qu'un beam search fait
// main, au prix d'une qualité un peu en retrait par rapport à Google Translate.
//
// Piège trouvé en le testant avec un vrai modèle avant intégration : sur la
// branche "cache" du décodeur fusionné, les sorties present.*.encoder.key/value
// sont une constante factice codée dans le graphe exporté (pas les vraies
// valeurs). Le cache d'attention croisée doit donc être figé une fois pour
// toutes à partir de la sortie du tout premier pas (use_cache_branch=false),
// jamais mis à jour ensuite.
internal sealed class TranslationEngine : IDisposable
{
    private readonly InferenceSession _encoder;
    private readonly InferenceSession _decoder;
    private readonly SentencePieceTokenizer _sourceTokenizer;
    private readonly Dictionary<string, int> _vocab;
    private readonly Dictionary<int, string> _inverseVocab;
    private readonly int _numLayers;
    private readonly int _numHeads;
    private readonly int _headDim;
    private readonly int _padId;
    private readonly int _eosId;
    private readonly int _decoderStartId;
    private readonly int _unkId;

    private TranslationEngine(
        InferenceSession encoder, InferenceSession decoder, SentencePieceTokenizer sourceTokenizer,
        Dictionary<string, int> vocab, int numLayers, int numHeads, int headDim,
        int padId, int eosId, int decoderStartId)
    {
        _encoder = encoder;
        _decoder = decoder;
        _sourceTokenizer = sourceTokenizer;
        _vocab = vocab;
        _inverseVocab = vocab.GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.First().Key);
        _numLayers = numLayers;
        _numHeads = numHeads;
        _headDim = headDim;
        _padId = padId;
        _eosId = eosId;
        _decoderStartId = decoderStartId;
        _unkId = vocab.TryGetValue("<unk>", out var u) ? u : 0;
    }

    public static async Task<TranslationEngine> LoadAsync(string modelDir)
    {
        var vocabJson = await File.ReadAllTextAsync(Path.Combine(modelDir, "vocab.json"));
        var vocab = JsonSerializer.Deserialize<Dictionary<string, int>>(vocabJson)
            ?? throw new InvalidDataException("vocab.json invalide");

        var genConfigPath = Path.Combine(modelDir, "generation_config.json");
        int padId = 0, eosId = 0, decoderStartId = 0;
        if (File.Exists(genConfigPath))
        {
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(genConfigPath));
            padId = doc.RootElement.TryGetProperty("pad_token_id", out var p) ? p.GetInt32() : 0;
            eosId = doc.RootElement.TryGetProperty("eos_token_id", out var e) ? e.GetInt32() : 0;
            decoderStartId = doc.RootElement.TryGetProperty("decoder_start_token_id", out var d) ? d.GetInt32() : padId;
        }

        var sessionOptions = new SessionOptions
        {
            // Le planificateur mémoire d'ONNX Runtime réutilise mal un buffer
            // pour ce graphe (dimensions dynamiques + noeud If) : désactivé par
            // prudence, comme validé lors du prototypage Python.
            EnableMemoryPattern = false,
        };
        var encoder = new InferenceSession(Path.Combine(modelDir, "onnx", "encoder_model_quantized.onnx"), sessionOptions);
        var decoder = new InferenceSession(Path.Combine(modelDir, "onnx", "decoder_model_merged_quantized.onnx"), sessionOptions);

        using var sourceSpmStream = File.OpenRead(Path.Combine(modelDir, "source.spm"));
        var sourceTokenizer = SentencePieceTokenizer.Create(sourceSpmStream, false, false, null);

        var (numLayers, numHeads, headDim) = InspectDecoderShape(decoder);

        return new TranslationEngine(encoder, decoder, sourceTokenizer, vocab, numLayers, numHeads, headDim, padId, eosId, decoderStartId);
    }

    private static (int NumLayers, int NumHeads, int HeadDim) InspectDecoderShape(InferenceSession decoder)
    {
        var layerIndices = decoder.InputMetadata.Keys
            .Select(name => System.Text.RegularExpressions.Regex.Match(name, @"^past_key_values\.(\d+)\.decoder\.key$"))
            .Where(m => m.Success)
            .Select(m => int.Parse(m.Groups[1].Value))
            .ToList();
        var numLayers = layerIndices.Count > 0 ? layerIndices.Max() + 1 : 6;

        var dims = decoder.InputMetadata["past_key_values.0.decoder.key"].Dimensions;
        var numHeads = dims.Length > 1 && dims[1] > 0 ? dims[1] : 8;
        var headDim = dims.Length > 3 && dims[3] > 0 ? dims[3] : 64;
        return (numLayers, numHeads, headDim);
    }

    public Task<string> TranslateAsync(string text, int maxTokens = 128, CancellationToken cancellationToken = default) =>
        Task.Run(() => Translate(text, maxTokens, cancellationToken), cancellationToken);

    private string Translate(string text, int maxTokens, CancellationToken cancellationToken)
    {
        var inputIds = EncodeSource(text);
        var attentionMask = new long[inputIds.Length];
        Array.Fill(attentionMask, 1L);

        using var encoderResults = _encoder.Run(new[]
        {
            NamedOnnxValue.CreateFromTensor("input_ids", ToTensor2D(inputIds)),
            NamedOnnxValue.CreateFromTensor("attention_mask", ToTensor2D(attentionMask)),
        });
        var encoderHiddenStates = encoderResults.First(v => v.Name == "last_hidden_state").AsTensor<float>().ToDenseTensor();

        var decoderPast = new Dictionary<string, DenseTensor<float>>();
        for (var i = 0; i < _numLayers; i++)
        {
            decoderPast[$"past_key_values.{i}.decoder.key"] = new DenseTensor<float>([1, _numHeads, 0, _headDim]);
            decoderPast[$"past_key_values.{i}.decoder.value"] = new DenseTensor<float>([1, _numHeads, 0, _headDim]);
            decoderPast[$"past_key_values.{i}.encoder.key"] = new DenseTensor<float>([1, _numHeads, 0, _headDim]);
            decoderPast[$"past_key_values.{i}.encoder.value"] = new DenseTensor<float>([1, _numHeads, 0, _headDim]);
        }

        var outputIds = new List<int>();
        var nextTokenId = _decoderStartId;
        var useCacheBranch = false;

        for (var step = 0; step < maxTokens; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", ToTensor2D(new long[] { nextTokenId })),
                NamedOnnxValue.CreateFromTensor("encoder_attention_mask", ToTensor2D(attentionMask)),
                NamedOnnxValue.CreateFromTensor("encoder_hidden_states", encoderHiddenStates),
                NamedOnnxValue.CreateFromTensor("use_cache_branch", new DenseTensor<bool>(new[] { useCacheBranch }, [1])),
            };
            foreach (var (name, tensor) in decoderPast)
                inputs.Add(NamedOnnxValue.CreateFromTensor(name, tensor));

            using var results = _decoder.Run(inputs);
            var resultMap = results.ToDictionary(v => v.Name, v => v.AsTensor<float>().ToDenseTensor());

            var logits = resultMap["logits"];
            var vocabSize = logits.Dimensions[2];
            var lastStep = logits.Dimensions[1] - 1;
            var bestId = -1;
            var bestScore = float.NegativeInfinity;
            for (var v = 0; v < vocabSize; v++)
            {
                if (v == _padId) continue;
                var score = logits[0, lastStep, v];
                if (score > bestScore) { bestScore = score; bestId = v; }
            }

            if (bestId == _eosId || bestId < 0) break;
            outputIds.Add(bestId);

            var wasFirstStep = !useCacheBranch;
            for (var i = 0; i < _numLayers; i++)
            {
                decoderPast[$"past_key_values.{i}.decoder.key"] = resultMap[$"present.{i}.decoder.key"];
                decoderPast[$"past_key_values.{i}.decoder.value"] = resultMap[$"present.{i}.decoder.value"];
                if (wasFirstStep)
                {
                    // Le cache d'attention croisée n'est réel qu'à ce premier pas
                    // (voir le commentaire de tête de fichier) : figé ensuite.
                    decoderPast[$"past_key_values.{i}.encoder.key"] = resultMap[$"present.{i}.encoder.key"];
                    decoderPast[$"past_key_values.{i}.encoder.value"] = resultMap[$"present.{i}.encoder.value"];
                }
            }

            nextTokenId = bestId;
            useCacheBranch = true;
        }

        return DecodeTarget(outputIds);
    }

    private int[] EncodeSource(string text)
    {
        var tokens = _sourceTokenizer.EncodeToTokens(text, out _, false, false);
        var ids = new int[tokens.Count + 1];
        for (var i = 0; i < tokens.Count; i++)
            ids[i] = _vocab.TryGetValue(tokens[i].Value, out var id) ? id : _unkId;
        ids[^1] = _eosId;
        return ids;
    }

    private string DecodeTarget(List<int> ids)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var id in ids)
            sb.Append(_inverseVocab.TryGetValue(id, out var piece) ? piece : "<unk>");
        return sb.ToString().Replace('▁', ' ').Trim();
    }

    private static DenseTensor<long> ToTensor2D(long[] row) => new(row, [1, row.Length]);

    private static DenseTensor<long> ToTensor2D(int[] row)
    {
        var longs = new long[row.Length];
        for (var i = 0; i < row.Length; i++) longs[i] = row[i];
        return new DenseTensor<long>(longs, [1, row.Length]);
    }

    public void Dispose()
    {
        _encoder.Dispose();
        _decoder.Dispose();
    }
}
