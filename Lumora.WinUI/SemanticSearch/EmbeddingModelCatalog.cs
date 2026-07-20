using Lumora.WinUI.ModelDownload;

namespace Lumora.WinUI.SemanticSearch;

// Modele d'embedding : multilingual-e5-small (intfloat), export ONNX quantifie
// depuis l'org Xenova sur Hugging Face - meme source et meme principe que les
// modeles de traduction (TranslationModelCatalog) : fichiers generiques, aucune
// donnee utilisateur envoyee pour les telecharger. 384 dimensions, ~118 Mo pour
// le modele quantifie int8, multilingue (couvre le francais).
//
// Convention d'usage E5 (verifiee sur le model card intfloat/multilingual-e5-small
// et le format d'export Xenova standard) : prefixer "query: " pour une requete
// de recherche et "passage: " pour un texte indexe, pooling par moyenne masquee
// sur last_hidden_state (PAS le token [CLS]), puis normalisation L2. Cette
// convention n'a pas ete verifiee par execution reelle avant la premiere
// utilisation ; EmbeddingEngine expose une verification de coherence au premier
// chargement (voir son commentaire d'en-tete).
internal static class EmbeddingModelCatalog
{
    public const string HuggingFaceRepo = "Xenova/multilingual-e5-small";
    public const int EmbeddingDimensions = 384;
    public const string QueryPrefix = "query: ";
    public const string PassagePrefix = "passage: ";

    // Fichiers necessaires a l'inference, relatifs a la racine du depot Hugging
    // Face, avec empreinte SHA256 verifiee (voir ModelDownload/ModelIntegrity.cs)
    // - obtenue en telechargeant reellement ces fichiers le 2026-07-20, en meme
    // temps que la verification empirique du moteur d'embedding (voir
    // EmbeddingEngine.cs).
    public static readonly IReadOnlyList<ModelFile> RequiredFiles =
    [
        new("config.json", "cb99455288675345e1a4f411438d5d0adbba5fbd3a67ea4fb03c015433b996c1"),
        new("tokenizer.json", "0b44a9d7b51c3c62626640cda0e2c2f70fdacdc25bbbd68038369d14ebdf4c39"),
        new("tokenizer_config.json", "a1d6bc8734a6f635dc158508bef000f8e2e5a759c7d92f984b2c86e5ff53425b"),
        new("special_tokens_map.json", "d05497f1da52c5e09554c0cd874037a083e1dc1b9cfd48034d1c717f1afc07a7"),
        new("sentencepiece.bpe.model", "cfc8146abe2a0488e9e2a0c56de7952f7c11ab059eca145a0a727afce0db2865"),
        new("onnx/model_quantized.onnx", "f80102d3f2a1229f387d3c81909990d8945513e347b0eab049f7de3c6f98c193"),
    ];
}
