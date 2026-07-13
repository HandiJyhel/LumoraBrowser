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

    // Fichiers nécessaires à l'inférence, relatifs à la racine du dépôt Hugging Face.
    public static readonly string[] RequiredFiles =
    [
        "vocab.json",
        "source.spm",
        "target.spm",
        "onnx/encoder_model_quantized.onnx",
        "onnx/decoder_model_merged_quantized.onnx",
    ];
}
