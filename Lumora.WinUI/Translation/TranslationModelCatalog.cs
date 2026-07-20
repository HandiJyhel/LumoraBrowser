using Lumora.WinUI.ModelDownload;

namespace Lumora.WinUI.Translation;

// Catalogue des paires de langues supportées. Modèles OPUS-MT (Helsinki-NLP),
// export ONNX quantifié depuis Hugging Face (org Xenova) : traduction neuronale
// légère, exécutée entièrement en local via ONNX Runtime. Aucune donnée n'est
// envoyée à un serveur externe pour traduire — seul le fichier de modèle
// (générique, pas de contenu utilisateur) est téléchargé une fois puis mis en cache.
internal sealed record TranslationModelInfo(string SourceLang, string TargetLang, string HuggingFaceRepo)
{
    public string PairId => $"{SourceLang}-{TargetLang}";
}

internal static class TranslationModelCatalog
{
    public static readonly IReadOnlyList<TranslationModelInfo> SupportedPairs =
    [
        new("en", "fr", "Xenova/opus-mt-en-fr"),
        new("fr", "en", "Xenova/opus-mt-fr-en"),
    ];

    public static TranslationModelInfo? Find(string sourceLang, string targetLang) =>
        SupportedPairs.FirstOrDefault(p => p.SourceLang == sourceLang && p.TargetLang == targetLang);

    // Fichiers necessaires a l'inference, relatifs a la racine du depot Hugging
    // Face. vocab.json/source.spm/target.spm sont partages par construction
    // entre les deux sens de traduction (memes fichiers, roles source/cible
    // inverses) : empreintes SHA256 obtenues en telechargeant reellement ces
    // fichiers le 2026-07-20 (voir ModelDownload/ModelIntegrity.cs).
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<ModelFile>> RequiredFilesByPair =
        new Dictionary<string, IReadOnlyList<ModelFile>>
        {
            ["en-fr"] =
            [
                new("vocab.json", "f2ba9c69ae20f96b8bd821239a9152be422394f980350b77907cffc183db5f2d"),
                new("source.spm", "173e9f493a668fe396d599e28d414a201193094e6ffd7a4678e5aab0f6d3d838"),
                new("target.spm", "78d0e717c77053f1c4b856d8661d9cb87c64f083a35418c087b9146300e4f585"),
                new("onnx/encoder_model_quantized.onnx", "0a81bdba62f53223740a8c6c6f58e716eba8ea1f92dfa569caf27ed5e3b6a0f7"),
                new("onnx/decoder_model_merged_quantized.onnx", "333b244bce16023df04541c8cf9fd60aec9b0569da393c4b831d561897b0bda8"),
            ],
            ["fr-en"] =
            [
                new("vocab.json", "f2ba9c69ae20f96b8bd821239a9152be422394f980350b77907cffc183db5f2d"),
                new("source.spm", "78d0e717c77053f1c4b856d8661d9cb87c64f083a35418c087b9146300e4f585"),
                new("target.spm", "173e9f493a668fe396d599e28d414a201193094e6ffd7a4678e5aab0f6d3d838"),
                new("onnx/encoder_model_quantized.onnx", "e727cb26ac6bf816394c49671af69c9ae5798868fed1e40849872415dffc1772"),
                new("onnx/decoder_model_merged_quantized.onnx", "73bc7ac8e29c42e6f212ebcc29a2991d3646a04aabb642c0036aa54b40e4e1a9"),
            ],
        };
}
