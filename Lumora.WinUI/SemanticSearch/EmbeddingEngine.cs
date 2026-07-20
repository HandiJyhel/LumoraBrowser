using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace Lumora.WinUI.SemanticSearch;

// Moteur d'embedding local (multilingual-e5-small, encodeur XLM-RoBERTa, ONNX).
// Un seul passage encodeur (pas de generation autoregressive) : beaucoup plus
// simple que TranslationEngine, sur le meme principe (Microsoft.ML.OnnxRuntime
// direct + Microsoft.ML.Tokenizers), sans passer par OnnxRuntimeGenAI (reserve
// a SearchAssist/Phi-3).
//
// Decalage de tokenisation XLM-RoBERTa (piece sentencepiece brute -> id final
// HF : <s>=0, </s>=2, <unk>=3, puis piece_id+1 pour le reste - convention
// "fairseq" standard de toute la famille XLM-R) : VERIFIE empiriquement avant
// integration via un harnais autonome (meme algorithme, hors Lumora,
// 2026-07-20) qui telecharge reellement ce modele et compare des similarites
// cosinus sur des phrases francaises/anglaises connues. Resultats : phrases
// proches en sens ~0.88-0.94, phrases sans rapport ~0.78-0.85, alignement
// multilingue fr/en coherent (~0.94 pour une meme phrase traduite). L'inference
// se fait sequence par sequence SANS padding (jamais de pad_token_id utilise),
// et token_type_ids est fourni uniquement si le graphe ONNX le demande
// (confirme present pour ce modele via InputMetadata). Le garde-fou
// MainWindow.EnsureSemanticSelfCheckAsync() rejoue un test equivalent au tout
// premier usage reel dans l'application, au cas ou un futur changement de
// modele romprait silencieusement cette hypothese.
internal sealed class EmbeddingEngine : IDisposable
{
    private const int BeginOfSentenceId = 0; // <s>  - constante XLM-R, identique pour tous les modeles de cette famille
    private const int EndOfSentenceId = 2;   // </s> - idem
    private const int UnknownId = 3;         // <unk> - idem (rarement atteint : impacte peu la qualite globale si jamais faux)
    private const int FairseqOffset = 1;     // final_id = raw_sentencepiece_id + FairseqOffset, pour les tokens de contenu
    private const int MaxTokens = 256;       // largement suffisant pour un extrait de page, evite un cout memoire/CPU inutile

    private readonly InferenceSession _session;
    private readonly SentencePieceTokenizer _tokenizer;
    private readonly bool _needsTokenTypeIds;

    private EmbeddingEngine(InferenceSession session, SentencePieceTokenizer tokenizer, bool needsTokenTypeIds)
    {
        _session = session;
        _tokenizer = tokenizer;
        _needsTokenTypeIds = needsTokenTypeIds;
    }

    public static Task<EmbeddingEngine> LoadAsync(string modelDir) => Task.Run(() =>
    {
        var sessionOptions = new SessionOptions { EnableMemoryPattern = false };
        var session = new InferenceSession(Path.Combine(modelDir, "onnx", "model_quantized.onnx"), sessionOptions);

        using var spmStream = File.OpenRead(Path.Combine(modelDir, "sentencepiece.bpe.model"));
        // addBeginOfSentence/addEndOfSentence a false : on emet <s>/</s> nous-memes
        // avec les ids finaux XLM-R (voir remarque de tete de fichier), plutot que
        // de laisser le tokenizer les ajouter avec des ids potentiellement bruts.
        var tokenizer = SentencePieceTokenizer.Create(spmStream, addBeginOfSentence: false, addEndOfSentence: false, specialTokens: null);

        var needsTokenTypeIds = session.InputMetadata.ContainsKey("token_type_ids");

        return new EmbeddingEngine(session, tokenizer, needsTokenTypeIds);
    });

    // isQuery=true prefixe "query: " (texte tape par l'utilisateur), false
    // prefixe "passage: " (contenu de page indexe) - convention du modele E5,
    // les deux types de textes ne sont pas interchangeables pour ce modele.
    public Task<float[]> EmbedAsync(string text, bool isQuery, CancellationToken cancellationToken = default) =>
        Task.Run(() => Embed(text, isQuery, cancellationToken), cancellationToken);

    private float[] Embed(string text, bool isQuery, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var prefixed = (isQuery ? EmbeddingModelCatalog.QueryPrefix : EmbeddingModelCatalog.PassagePrefix) + text;
        var ids = Encode(prefixed);

        var inputIds = new DenseTensor<long>(ids.Select(i => (long)i).ToArray(), [1, ids.Length]);
        var attentionMask = new DenseTensor<long>(Enumerable.Repeat(1L, ids.Length).ToArray(), [1, ids.Length]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask),
        };
        if (_needsTokenTypeIds)
        {
            var tokenTypeIds = new DenseTensor<long>(new long[ids.Length], [1, ids.Length]);
            inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIds));
        }

        using var results = _session.Run(inputs);
        var hidden = results.First(v => v.Name == "last_hidden_state").AsTensor<float>().ToDenseTensor();

        return MeanPoolAndNormalize(hidden, ids.Length);
    }

    // Moyenne masquee sur la dimension sequence (ici tous les tokens comptent,
    // aucun padding puisqu'une seule sequence est traitee a la fois), puis
    // normalisation L2 - convention standard des modeles E5 (PAS le token [CLS]).
    private static float[] MeanPoolAndNormalize(Tensor<float> hidden, int sequenceLength)
    {
        var hiddenSize = hidden.Dimensions[2];
        var pooled = new float[hiddenSize];
        for (var t = 0; t < sequenceLength; t++)
        {
            for (var h = 0; h < hiddenSize; h++)
            {
                pooled[h] += hidden[0, t, h];
            }
        }

        for (var h = 0; h < hiddenSize; h++)
        {
            pooled[h] /= sequenceLength;
        }

        var norm = MathF.Sqrt(pooled.Sum(v => v * v));
        if (norm > 1e-9f)
        {
            for (var h = 0; h < hiddenSize; h++)
            {
                pooled[h] /= norm;
            }
        }

        return pooled;
    }

    private int[] Encode(string text)
    {
        var tokens = _tokenizer.EncodeToTokens(text, out _, considerNormalization: true, considerPreTokenization: true);
        var budget = Math.Max(1, MaxTokens - 2); // place pour <s> et </s>
        var contentIds = tokens
            .Take(budget)
            .Select(t => t.Id == 0 ? UnknownId : t.Id + FairseqOffset)
            .ToArray();

        var ids = new int[contentIds.Length + 2];
        ids[0] = BeginOfSentenceId;
        Array.Copy(contentIds, 0, ids, 1, contentIds.Length);
        ids[^1] = EndOfSentenceId;
        return ids;
    }

    public void Dispose()
    {
        _session.Dispose();
    }
}
